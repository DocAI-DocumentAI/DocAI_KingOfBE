using AutoMapper;
using MassTransit;
using Notification.API.Constants;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz.Impl.AdoJobStore;
using Shared.Command;
using Shared.DTOs;
using Shared.Models;

namespace Notification.API.Services.Implement
{
    public class DocumentScanService : IDocumentScanService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<DocumentScanService> _logger;
        private readonly INotificationService _notificationService;
        private readonly INotificationConfigService _configService;
        private readonly IRequestClient<GetExpiringDocumentsCommand> _documentClient;

        public DocumentScanService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<DocumentScanService> logger,
            INotificationService notificationService,
            INotificationConfigService configService,
            IRequestClient<GetExpiringDocumentsCommand> documentClient)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
            _configService = configService;
            _documentClient = documentClient;
        }

        public async Task ScanAndProcessDocumentsAsync()
        {
            var scanId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("Starting document expiration scan - ScanId: {ScanId}", scanId);

            try
            {
                var config = await _configService.GetNotificationConfigAsync();
                if (!config.QuartzEnabled)
                {
                    _logger.LogInformation("Document scanning is disabled - ScanId: {ScanId}", scanId);
                    return;
                }

                var warningDate = DateTime.UtcNow.AddDays(config.WarningThresholdDays);
                _logger.LogInformation("Fetching documents expiring before {WarningDate} - ScanId: {ScanId}",
                    warningDate, scanId);

                var documents = await GetExpiringDocumentsAsync(warningDate);

                if (!documents.Any())
                {
                    _logger.LogInformation("No documents require expiration notifications - ScanId: {ScanId}", scanId);
                    return;
                }

                _logger.LogInformation("Found {DocumentCount} documents to process - ScanId: {ScanId}",
                    documents.Count, scanId);

                await ProcessDocumentsAsync(documents, scanId);
                _logger.LogInformation("Document expiration scan completed successfully - ScanId: {ScanId}", scanId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document expiration scan - ScanId: {ScanId}", scanId);
                throw;
            }
        }

        private async Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                var response = await _documentClient.GetResponse<GetExpiringDocumentsResponse>(
                    new GetExpiringDocumentsCommand { WarningDate = warningDate },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    _logger.LogInformation("Retrieved {Count} documents for expiration check",
                        response.Message.Documents.Count);
                    return response.Message.Documents;
                }

                _logger.LogWarning("Document service returned error: {Error}", response.Message.ErrorMessage);
                return new List<DocumentExpirationDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogError("Timeout getting expiring documents from Document service");
                return new List<DocumentExpirationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expiring documents");
                return new List<DocumentExpirationDto>();
            }
        }

        private async Task ProcessDocumentsAsync(List<DocumentExpirationDto> documents, string scanId)
        {
            var processedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            // ✅ Filter valid documents
            var validDocuments = documents.Where(doc =>
                doc.EffectiveFrom.HasValue &&
                doc.EffectiveUntil.HasValue).ToList();

            _logger.LogInformation("Found {TotalDocs} documents, {ValidDocs} have both EffectiveFrom and EffectiveUntil - ScanId: {ScanId}",
                documents.Count, validDocuments.Count, scanId);

            var expiredDocs = new List<DocumentExpirationDto>();
            var nearExpiredDocs = new List<DocumentExpirationDto>();
            var today = DateTime.UtcNow.Date;

            // ✅ Classify documents
            var config = await _configService.GetNotificationConfigAsync();
            var warningThreshold = config.WarningThresholdDays;

            foreach (var doc in validDocuments)
            {
                var effectiveUntilDate = doc.EffectiveUntil!.Value.Date;
                var daysUntilExpiry = (effectiveUntilDate - today).Days;

                if (daysUntilExpiry < 0)
                {
                    expiredDocs.Add(doc);
                }
                else if (daysUntilExpiry <= warningThreshold)
                {
                    nearExpiredDocs.Add(doc);
                }
            }

            _logger.LogInformation("Processing {ExpiredCount} expired and {NearExpiredCount} near-expired documents - ScanId: {ScanId}",
                expiredDocs.Count, nearExpiredDocs.Count, scanId);

            // ✅ 1. Process expired documents immediately (unchanged)
            foreach (var doc in expiredDocs)
            {
                try
                {
                    if (await HasRecentNotificationAsync(doc, NotificationType.Expired, 1))
                    {
                        skippedCount++;
                        continue;
                    }

                    await _notificationService.ProcessExpiredDocumentNotification(doc);
                    processedCount++;
                    _logger.LogInformation("Processed expired notification for {DocId}/{Version} - ScanId: {ScanId}",
                        doc.DocumentId, doc.Version, scanId);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Error processing expired document {DocId}/{Version} - ScanId: {ScanId}",
                        doc.DocumentId, doc.Version, scanId);
                }
            }

            // ✅ 2. Process near-expired documents with weekly grouping
            if (nearExpiredDocs.Any())
            {
                // ✅ SIMPLE: Check if today is Monday (weekly notification day)
                var isMonday = DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday;

                if (isMonday)
                {
                    _logger.LogInformation("Monday detected - processing weekly grouped notifications for {Count} near-expired documents",
                        nearExpiredDocs.Count);

                    // ✅ Group by department for weekly notifications
                    var departmentGroups = nearExpiredDocs.GroupBy(d => new { d.DepartmentId, d.DepartmentName });

                    foreach (var group in departmentGroups)
                    {
                        try
                        {
                            var deptDocuments = group.ToList();

                            // ✅ Check if weekly notification already sent for this department
                            if (await HasRecentWeeklyNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), 7))
                            {
                                _logger.LogDebug("Weekly notification already sent for department {DeptName} in last 7 days",
                                    group.Key.DepartmentName);
                                skippedCount += deptDocuments.Count;
                                continue;
                            }

                            await _notificationService.ProcessWeeklyGroupedNotificationAsync(deptDocuments, group.Key.DepartmentName);
                            processedCount += deptDocuments.Count;

                            _logger.LogInformation("Processed weekly grouped notification for department {DeptName} with {DocCount} documents - ScanId: {ScanId}",
                                group.Key.DepartmentName, deptDocuments.Count, scanId);
                        }
                        catch (Exception ex)
                        {
                            errorCount += group.Count();
                            _logger.LogError(ex, "Error processing weekly grouped notification for department {DeptName} - ScanId: {ScanId}",
                                group.Key.DepartmentName, scanId);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Not Monday - skipping near-expired documents (will be processed on Monday as grouped notification)");
                    skippedCount += nearExpiredDocs.Count;
                }
            }

            _logger.LogInformation("Scan completed - Processed: {Processed}, Skipped: {Skipped}, Errors: {Errors} - ScanId: {ScanId}",
                processedCount, skippedCount, errorCount, scanId);
        }

        private async Task<bool> HasRecentWeeklyNotificationForDepartmentAsync(string departmentId, int days)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = DateTime.UtcNow.AddDays(-days);

                return await logRepo.AnyAsync(l =>
                    l.DocumentId == "WEEKLY_GROUP" &&
                    l.DocumentVersion == departmentId &&
                    l.NotificationType == NotificationType.General &&
                    l.IsSent == true &&
                    l.SentAt >= cutoffTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking recent weekly notifications for department {DepartmentId}", departmentId);
                return false;
            }
        }
        private async Task<bool> HasRecentNotificationAsync(DocumentExpirationDto doc, NotificationType type, int hours)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);

                var hasRecent = await logRepo.AnyAsync(l =>
                    l.DocumentId == doc.DocumentId &&
                    l.DocumentVersion == doc.Version &&
                    l.NotificationType == type &&
                    l.IsSent == true &&
                    l.SentAt >= cutoffTime);

                if (hasRecent)
                {
                    _logger.LogDebug("{NotificationType} notification already sent for {DocId}/{Version} in last {Hours}h",
                        type, doc.DocumentId, doc.Version, hours);
                }

                return hasRecent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking recent notifications for {DocId}/{Version}/{Type}",
                    doc.DocumentId, doc.Version, type);
                return false; // Default to false to ensure notifications are sent
            }
        }
    }
}