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
                var allDocuments = await GetAllDocumentsFromServiceAsync();
                var today = DateTime.UtcNow.Date;

                var expiredDocs = allDocuments.Where(doc =>
                    doc.EffectiveFrom.HasValue &&
                    doc.EffectiveUntil.HasValue &&
                    doc.EffectiveUntil.Value.Date < today).ToList();

                _logger.LogInformation("Found {Count} expired documents", expiredDocs.Count);
                return expiredDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired documents");
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
                var allDocuments = await GetAllDocumentsFromServiceAsync();
                var today = DateTime.UtcNow.Date;
                var warningDate = today.AddDays(config.WarningThresholdDays);

                var nearExpiredDocs = allDocuments.Where(doc =>
                    doc.EffectiveFrom.HasValue &&
                    doc.EffectiveUntil.HasValue &&
                    doc.EffectiveUntil.Value.Date >= today &&
                    doc.EffectiveUntil.Value.Date <= warningDate).ToList();

                _logger.LogInformation("Found {Count} near-expired documents", nearExpiredDocs.Count);
                return nearExpiredDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting near-expired documents");
                return new List<DocumentExpirationDto>();
            }
        }

        public async Task ProcessNearExpiredDocumentsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            var config = await _configService.GetNotificationConfigAsync();

            _logger.LogInformation("Processing {Count} near-expired documents with mode: {Mode} - JobId: {JobId}",
                documents.Count, config.NearExpiredMode, jobId);

            switch (config.NearExpiredMode)
            {
                case NotificationMode.Individual:
                    await ProcessIndividualNotificationsAsync(documents, jobId);
                    break;

                case NotificationMode.Weekly:
                    await ProcessWeeklyGroupedNotificationsAsync(documents, jobId);
                    break;

                case NotificationMode.Daily:
                    await ProcessDailyGroupedNotificationsAsync(documents, jobId);
                    break;

                default:
                    _logger.LogWarning("Unknown notification mode: {Mode} - JobId: {JobId}", config.NearExpiredMode, jobId);
                    break;
            }
        }

        // Keep for backward compatibility
        public async Task ScanAndProcessDocumentsAsync()
        {
            var scanId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("Starting legacy document expiration scan - ScanId: {ScanId}", scanId);

            try
            {
                var config = await _configService.GetNotificationConfigAsync();
                if (!config.QuartzEnabled)
                {
                    _logger.LogInformation("Document scanning is disabled - ScanId: {ScanId}", scanId);
                    return;
                }

                if (config.EnableExpiredNotifications)
                {
                    var expiredDocs = await GetExpiredDocumentsAsync();
                    await ProcessExpiredDocumentsDirectlyAsync(expiredDocs, scanId);
                }

                if (config.EnableNearExpiredNotifications)
                {
                    var nearExpiredDocs = await GetNearExpiredDocumentsAsync();
                    await ProcessNearExpiredDocumentsAsync(nearExpiredDocs, scanId);
                }

                _logger.LogInformation("Legacy document expiration scan completed - ScanId: {ScanId}", scanId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during legacy document expiration scan - ScanId: {ScanId}", scanId);
                throw;
            }
        }

        private async Task ProcessExpiredDocumentsDirectlyAsync(List<DocumentExpirationDto> expiredDocs, string scanId)
        {
            _logger.LogInformation("Processing {Count} expired documents directly - ScanId: {ScanId}",
                expiredDocs.Count, scanId);

            var processedCount = 0;
            var skippedCount = 0;

            foreach (var doc in expiredDocs)
            {
                try
                {
                    if (await HasRecentNotificationAsync(doc, NotificationType.Expired, 24))
                    {
                        skippedCount++;
                        continue;
                    }

                    await _notificationService.ProcessExpiredDocumentNotification(doc);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing expired document {DocId}/{Version}",
                        doc.DocumentId, doc.Version);
                }
            }

            _logger.LogInformation("Expired documents processing completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
        }

        private async Task ProcessIndividualNotificationsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Sending individual notifications for {Count} documents - JobId: {JobId}",
                documents.Count, jobId);

            var processedCount = 0;
            var skippedCount = 0;

            foreach (var doc in documents)
            {
                try
                {
                    if (await HasRecentNotificationAsync(doc, NotificationType.NearingExpiration, 24))
                    {
                        skippedCount++;
                        continue;
                    }

                    await _notificationService.ProcessNearingExpirationNotification(doc);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing individual notification for {DocId}", doc.DocumentId);
                    skippedCount++;
                }
            }

            _logger.LogInformation("Individual notifications completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
        }

        private async Task ProcessWeeklyGroupedNotificationsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            _logger.LogInformation("Sending weekly grouped notifications for {Count} documents - JobId: {JobId}",
                documents.Count, jobId);

            var departmentGroups = documents.GroupBy(d => new { d.DepartmentId, d.DepartmentName });
            var processedCount = 0;
            var skippedCount = 0;

            foreach (var group in departmentGroups)
            {
                try
                {
                    var deptDocuments = group.ToList();

                    if (await HasRecentWeeklyNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), 7))
                    {
                        skippedCount += deptDocuments.Count;
                        continue;
                    }

                    await _notificationService.ProcessWeeklyGroupedNotificationAsync(deptDocuments, group.Key.DepartmentName);
                    processedCount += deptDocuments.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing weekly notification for department {DeptName}",
                        group.Key.DepartmentName);
                    skippedCount += group.Count();
                }
            }

            _logger.LogInformation("Weekly notifications completed - Processed: {Processed}, Skipped: {Skipped}",
                processedCount, skippedCount);
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

                    if (await HasRecentWeeklyNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), 1))
                    {
                        skippedCount += deptDocuments.Count;
                        continue;
                    }

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

        private async Task<List<DocumentExpirationDto>> GetAllDocumentsFromServiceAsync()
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                var response = await _documentClient.GetResponse<GetExpiringDocumentsResponse>(
                    new GetExpiringDocumentsCommand { WarningDate = DateTime.UtcNow.AddDays(365) },
                    timeout.Token);

                if (response.Message.Success)
                {
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

        private async Task<bool> HasRecentNotificationAsync(DocumentExpirationDto doc, NotificationType type, int hours)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);

                return await logRepo.AnyAsync(l =>
                    l.DocumentId == doc.DocumentId &&
                    l.DocumentVersion == doc.Version &&
                    l.NotificationType == type &&
                    l.IsSent == true &&
                    l.SentAt >= cutoffTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking recent notifications for {DocId}/{Version}",
                    doc.DocumentId, doc.Version);
                return false;
            }
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
    }
}
