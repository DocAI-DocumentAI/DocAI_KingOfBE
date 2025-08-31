using AutoMapper;
using MassTransit;
using Microsoft.Extensions.Logging;
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
using Shared.Utils;

namespace Notification.API.Services.Implement
{
    public class DocumentScanService : IDocumentScanService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<DocumentScanService> _logger;
        private readonly INotificationService _notificationService;
        private readonly INotificationConfigService _configService;
        private readonly IRequestClient<GetExpiringDocumentsCommand> _documentClient;
        private readonly INotificationLogService _logService;

        public DocumentScanService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<DocumentScanService> logger,
            INotificationService notificationService,
            INotificationConfigService configService,
            IRequestClient<GetExpiringDocumentsCommand> documentClient,
            INotificationLogService logService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
            _configService = configService;
            _documentClient = documentClient;
            _logService = logService;
        }

        public async Task<List<DocumentExpirationDto>> GetExpiredDocumentsAsync()
        {
            var config = await _configService.GetNotificationConfigAsync();

            if (!config.QuartzEnabled || !config.EnableExpiredNotifications)
            {
                _logger.LogInformation("Expired document notifications are disabled");
                return new List<DocumentExpirationDto>();
            }

            try
            {
                var allDocuments = await GetAllDocumentsFromServiceAsync(30);
                var vietnamToday = TimeZoneHelper.VietnamToday;

                // CHỈ lấy tài liệu hết hạn hôm nay để gửi notification
                var expiredDocs = allDocuments.Where(doc =>
                    doc.EffectiveFrom.HasValue &&
                    doc.EffectiveUntil.HasValue &&
                    IsDocumentExpiredToday(doc, vietnamToday))
                    .ToList();

                _logger.LogInformation("Found {Count} expired documents for notification", expiredDocs.Count);
                return expiredDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired documents for notification");
                return new List<DocumentExpirationDto>();
            }
        }
        public async Task<List<DocumentExpirationDto>> GetNearExpiredDocumentsAsync()
        {
            var config = await _configService.GetNotificationConfigAsync();

            if (!config.QuartzEnabled || !config.EnableNearExpiredNotifications)
            {
                _logger.LogInformation("Near-expired document notifications are disabled");
                return new List<DocumentExpirationDto>();
            }

            try
            {
                var allDocuments = await GetAllDocumentsFromServiceAsync(config.WarningThresholdDays);
                var vietnamToday = TimeZoneHelper.VietnamToday;
                var vietnamWarningStart = vietnamToday.AddDays(1); 
                var vietnamWarningEnd = vietnamToday.AddDays(config.WarningThresholdDays);

                var nearExpiredDocs = allDocuments.Where(doc =>
                    doc.EffectiveFrom.HasValue &&
                    doc.EffectiveUntil.HasValue &&
                    IsDocumentInNearExpirationWindow(doc, vietnamWarningStart, vietnamWarningEnd))
                    .ToList();

                return nearExpiredDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting near-expired documents");
                return new List<DocumentExpirationDto>();
            }
        }
        private bool IsDocumentExpiredToday(DocumentExpirationDto doc, DateTime vietnamToday)
        {
            try
            {
                if (!doc.EffectiveUntil.HasValue) return false;

                var daysFromToday = TimeZoneHelper.DaysFromTodayFromDatabase(doc.EffectiveUntil.Value);
                bool isExpiredToday = daysFromToday <= 0;

                _logger.LogDebug("Document {DocId} expiration check: EffectiveUntil={EffectiveUntil}, DaysFromToday={Days}, IsExpired={IsExpired}",
                    doc.DocumentId, doc.EffectiveUntil, daysFromToday, isExpiredToday);

                return isExpiredToday;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking expiration for document {DocId}", doc.DocumentId);
                return false;
            }
        }
        private bool IsDocumentInNearExpirationWindow(DocumentExpirationDto doc, DateTime vietnamWarningStart, DateTime vietnamWarningEnd)
        {
            try
            {
                var docUtc = DateTime.SpecifyKind(doc.EffectiveUntil.Value, DateTimeKind.Utc);
                var docVietnamDate = TimeZoneHelper.ConvertUtcToVietnam(docUtc).Date;

                bool isInWindow = docVietnamDate >= vietnamWarningStart && docVietnamDate <= vietnamWarningEnd;

                return isInWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking near-expiration for document {DocId}", doc.DocumentId);
                return false;
            }
        }

        public async Task ProcessNearExpiredDocumentsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            var config = await _configService.GetNotificationConfigAsync();
            var vietnamNow = TimeZoneHelper.VietnamNow;

            _logger.LogInformation("Processing {Count} near-expired documents with Daily mode at Vietnam time: {VietnamTime} - JobId: {JobId}",
                documents.Count, vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"), jobId);

            // CHỈ GỬI DAILY GROUPED NOTIFICATIONS
            await ProcessDailyGroupedNotificationsAsync(documents, jobId);
        }
        private async Task ProcessDailyGroupedExpiredNotificationsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Sending daily grouped EXPIRED notifications for {Count} documents - JobId: {JobId}",
               documents.Count, jobId);

            var departmentGroups = documents.GroupBy(d => new { d.DepartmentId, d.DepartmentName });
            var processedCount = 0;
            var skippedCount = 0;

            foreach (var group in departmentGroups)
            {
                try
                {
                    var deptDocuments = group.ToList();

                    // Check for recent expired grouped notification (daily check)
                    //if (await HasRecentGroupedNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), "EXPIRED_DAILY_GROUP", 1))
                    //{
                    //    skippedCount += deptDocuments.Count;
                    //    continue;
                    //}

                    // Send grouped expired notification
                    await _notificationService.ProcessDailyGroupedExpiredNotificationAsync(deptDocuments, group.Key.DepartmentName);
                    processedCount += deptDocuments.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing daily expired notification for department {DeptName}",
                        group.Key.DepartmentName);
                    skippedCount += group.Count();
                }
            }

            _logger.LogInformation("Daily expired notifications completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
        }
        public async Task<List<DocumentExpirationDto>> GetDocumentsForStatusUpdateAsync()
        {
            var config = await _configService.GetNotificationConfigAsync();

            if (!config.QuartzEnabled)
            {
                return new List<DocumentExpirationDto>();
            }

            try
            {
                // Lấy tài liệu hết hạn hôm nay cần update status
                var allDocuments = await GetAllDocumentsFromServiceAsync(30); // Lấy 30 ngày
                var vietnamToday = TimeZoneHelper.VietnamToday;

                var documentsToUpdate = allDocuments.Where(doc =>
                    doc.EffectiveFrom.HasValue &&
                    doc.EffectiveUntil.HasValue &&
                    doc.Status == "Approved" && // Chỉ update những tài liệu đang Approved
                    IsDocumentExpiredToday(doc, vietnamToday))
                    .ToList();

                _logger.LogInformation("Found {Count} documents requiring status update to Archived today",
                    documentsToUpdate.Count);

                return documentsToUpdate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents for status update");
                return new List<DocumentExpirationDto>();
            }
        }
        public async Task ProcessDocumentStatusUpdatesAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Processing status updates for {Count} expired documents - JobId: {JobId}",
          documents.Count, jobId);

            var successfullyUpdatedDocuments = new List<DocumentExpirationDto>();
            var errorCount = 0;

            // Step 1: Update document status
            foreach (var doc in documents)
            {
                try
                {
                    // Check if status was already updated recently (24h)
                    var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                    var cutoffTime = TimeZoneHelper.UtcNow.AddHours(-24);

                    var alreadyUpdated = await logRepo.AnyAsync(l =>
                        l.DocumentId == doc.DocumentId &&
                        l.DocumentVersion == doc.Version &&
                        l.NotificationType == NotificationType.General &&
                        l.Subject.Contains("Status Updated to Archived") &&
                        l.IsSent == true &&
                        l.SentAt >= cutoffTime);

                    if (alreadyUpdated)
                    {
                        _logger.LogDebug("Status already updated for document {DocId}/{Version} within last 24h",
                            doc.DocumentId, doc.Version);
                        continue;
                    }

                    // Try to update status via NotificationService
                    var updateSuccess = await _notificationService.UpdateExpiredDocumentStatusAsync(doc);

                    if (updateSuccess)
                    {
                        successfullyUpdatedDocuments.Add(doc);
                        _logger.LogInformation("Successfully updated status for document {DocId}/{Version}",
                            doc.DocumentId, doc.Version);
                    }
                    else
                    {
                        errorCount++;
                        _logger.LogError("Failed to update status for document {DocId}/{Version}",
                            doc.DocumentId, doc.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating status for expired document {DocId}/{Version}",
                        doc.DocumentId, doc.Version);
                    errorCount++;
                }
            }

            _logger.LogInformation("Document status updates completed - Successfully Updated: {Success}, Errors: {Errors}",
                successfullyUpdatedDocuments.Count, errorCount);

            // Step 2: Send expired notifications for successfully updated documents 
            // (using existing expired notification logic)
            if (successfullyUpdatedDocuments.Any())
            {
                _logger.LogInformation("Sending expired notifications for {Count} successfully updated documents - JobId: {JobId}",
                    successfullyUpdatedDocuments.Count, jobId);

                // Use existing ProcessDailyGroupedExpiredNotificationsAsync method
                await ProcessDailyGroupedExpiredNotificationsForUpdatedDocumentsAsync(successfullyUpdatedDocuments, jobId);
            }
            else
            {
                _logger.LogInformation("No documents were successfully updated, skipping expired notifications - JobId: {JobId}", jobId);
            }
        }
        private async Task ProcessDailyGroupedExpiredNotificationsForUpdatedDocumentsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Sending daily grouped EXPIRED notifications for {Count} status-updated documents - JobId: {JobId}",
       documents.Count, jobId);

            var departmentGroups = documents.GroupBy(d => new { d.DepartmentId, d.DepartmentName });
            var processedCount = 0;
            var skippedCount = 0;

            foreach (var group in departmentGroups)
            {
                try
                {
                    var deptDocuments = group.ToList();

                    // Check for recent expired grouped notification for this department today
                    if (await HasRecentGroupedNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), "EXPIRED_DAILY_GROUP", 24))
                    {
                        _logger.LogInformation("Expired notification already sent today for department {DeptName}, skipping",
                            group.Key.DepartmentName);
                        skippedCount += deptDocuments.Count;
                        continue;
                    }

                    // Send grouped expired notification using existing method
                    await _notificationService.ProcessDailyGroupedExpiredNotificationAsync(deptDocuments, group.Key.DepartmentName);
                    processedCount += deptDocuments.Count;

                    _logger.LogInformation("Successfully sent expired notification for department {DeptName} with {Count} updated documents",
                        group.Key.DepartmentName, deptDocuments.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing expired notification for department {DeptName}",
                        group.Key.DepartmentName);
                    skippedCount += group.Count();
                }
            }

            _logger.LogInformation("Expired notifications for updated documents completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
        }
        // Update the expired notification check method
        private async Task<bool> HasRecentGroupedNotificationForDepartmentAsync(string departmentId, string groupType, int hours)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = TimeZoneHelper.UtcNow.AddMinutes(-hours);

                var notificationType = groupType.Contains("EXPIRED")
                    ? NotificationType.Expired
                    : NotificationType.NearingExpiration;

                return await logRepo.AnyAsync(l =>
                    l.DocumentId == groupType &&
                    l.DocumentVersion == departmentId &&
                    l.NotificationType == notificationType &&
                    l.IsSent == true &&
                    l.SentAt >= cutoffTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking recent {GroupType} notifications for department {DepartmentId}",
                    groupType, departmentId);
                return false;
            }
        }
        private async Task ProcessDailyGroupedNotificationsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Sending daily grouped notifications for {Count} documents - JobId: {JobId}",
               documents.Count, jobId);

            var departmentGroups = documents.GroupBy(d => new { d.DepartmentId, d.DepartmentName });
            var processedCount = 0;
            var skippedCount = 0;

            foreach (var group in departmentGroups)
            {
                try
                {
                    var deptDocuments = group.ToList();

                    //if (await HasRecentGroupedNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), "DAILY_GROUP", 1))
                    //{
                    //    skippedCount += deptDocuments.Count;
                    //    continue;
                    //}

                    await _notificationService.ProcessDailyGroupedNotificationAsync(deptDocuments, group.Key.DepartmentName);
                    processedCount += deptDocuments.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing daily notification for department {DeptName}",
                        group.Key.DepartmentName);
                    skippedCount += group.Count();
                }
            }

            _logger.LogInformation("Daily notifications completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
        }

        private async Task<List<DocumentExpirationDto>> GetAllDocumentsFromServiceAsync(int warningDays)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                // Use runtime warningDays instead of hardcoded 365
                var warningDate = TimeZoneHelper.UtcNow.AddDays(warningDays);

                var response = await _documentClient.GetResponse<GetExpiringDocumentsResponse>(
                    new GetExpiringDocumentsCommand { WarningDate = warningDate },
                    timeout.Token);

                if (response.Message.Success)
                {
                    _logger.LogDebug("Retrieved {Count} documents from document service using runtime threshold {WarningDays} days",
                        response.Message.Documents.Count, warningDays);
                    return response.Message.Documents;
                }

                _logger.LogWarning("Document service returned error: {Error}", response.Message.ErrorMessage);
                return new List<DocumentExpirationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents from service");
                return new List<DocumentExpirationDto>();
            }
        }
    }
}
