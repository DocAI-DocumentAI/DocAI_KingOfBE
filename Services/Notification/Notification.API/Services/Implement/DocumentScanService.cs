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
        public async Task ProcessExpiredDocumentsAsync(List<DocumentExpirationDto> documents, string jobId)
        {
            var vietnamNow = TimeZoneHelper.VietnamNow;
            _logger.LogInformation("Processing {Count} expired documents for NOTIFICATION ONLY at Vietnam time: {VietnamTime} - JobId: {JobId}",
                documents.Count, vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"), jobId);

            await ProcessDailyGroupedExpiredNotificationsAsync(documents, jobId);
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
                    if (await HasRecentGroupedNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), "EXPIRED_DAILY_GROUP", 1))
                    {
                        skippedCount += deptDocuments.Count;
                        continue;
                    }

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

        //private async Task ProcessExpiredDocumentStatusUpdatesAsync(List<DocumentExpirationDto> documents, string jobId)
        //{
        //    _logger.LogInformation("Updating status to 'Archived' for {Count} expired documents - JobId: {JobId}",
        //        documents.Count, jobId);

        //    var successCount = 0;
        //    var errorCount = 0;

        //    foreach (var doc in documents)
        //    {
        //        try
        //        {
        //            // Check if status was already updated recently
        //            var logRepo = _unitOfWork.GetRepository<NotificationLog>();
        //            var cutoffTime = TimeZoneHelper.UtcNow.AddHours(-12); // Check last 12 hours

        //            var alreadyUpdated = await logRepo.AnyAsync(l =>
        //                l.DocumentId == doc.DocumentId &&
        //                l.DocumentVersion == doc.Version &&
        //                l.NotificationType == NotificationType.General &&
        //                l.Subject.Contains("Document Archived") &&
        //                l.IsSent == true &&
        //                l.SentAt >= cutoffTime);

        //            if (alreadyUpdated)
        //            {
        //                _logger.LogDebug("Status already updated for document {DocId}/{Version}", doc.DocumentId, doc.Version);
        //                continue;
        //            }

        //            // Update status to Archived via notification service
        //            await _notificationService.UpdateExpiredDocumentStatusAsync(doc);
        //            successCount++;
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error updating status for expired document {DocId}/{Version}",
        //                doc.DocumentId, doc.Version);
        //            errorCount++;
        //        }
        //    }

        //    _logger.LogInformation("Expired document status updates completed - Success: {Success}, Errors: {Errors}",
        //        successCount, errorCount);
        //}
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

            var successCount = 0;
            var errorCount = 0;

            foreach (var doc in documents)
            {
                try
                {
                    // Kiểm tra xem status đã được update chưa trong 24h qua
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

                    // Update status via NotificationService
                    await _notificationService.UpdateExpiredDocumentStatusAsync(doc);
                    successCount++;

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating status for expired document {DocId}/{Version}",
                        doc.DocumentId, doc.Version);
                    errorCount++;
                }
            }

            _logger.LogInformation("Document status updates completed - Success: {Success}, Errors: {Errors}",
                successCount, errorCount);
        }
        // Update the expired notification check method
        private async Task<bool> HasRecentGroupedNotificationForDepartmentAsync(string departmentId, string groupType, int days)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = TimeZoneHelper.UtcNow.AddDays(-days);

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
        // Keep for backward compatibility
        public async Task ScanAndProcessDocumentsAsync()
        {
            var scanId = Guid.NewGuid().ToString("N")[..8];
            var vietnamNow = TimeZoneHelper.VietnamNow;

            _logger.LogInformation("Starting legacy document expiration scan at Vietnam time: {VietnamTime} - ScanId: {ScanId}",
                vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"), scanId);

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

        public async Task ProcessExpiredDocumentsDirectlyAsync(List<DocumentExpirationDto> expiredDocs, string scanId)
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

                    if (await HasRecentGroupedNotificationForDepartmentAsync(group.Key.DepartmentId.ToString(), "DAILY_GROUP", 1))
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

        private async Task<bool> HasRecentNotificationAsync(DocumentExpirationDto doc, NotificationType type, int hours)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = TimeZoneHelper.UtcNow.AddHours(-hours); // ✅ Use unified helper

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
    }
}
