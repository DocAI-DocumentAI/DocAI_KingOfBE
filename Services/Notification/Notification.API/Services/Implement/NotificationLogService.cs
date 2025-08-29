using AutoMapper;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz.Impl.AdoJobStore;
using Quartz.Util;
using System.Linq.Expressions;

namespace Notification.API.Services.Implement
{
    public class NotificationLogService : INotificationLogService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<NotificationLogService> _logger;
        private readonly INotificationConfigService _configService; // ADD: Direct config service

        public NotificationLogService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            ILogger<NotificationLogService> logger,
            INotificationConfigService configService) // ADD: Inject config service
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _configService = configService;
        }

        public async Task CreateLogAsync(NotificationLog log)
        {
            try
            {
                await _unitOfWork.GetRepository<NotificationLog>().InsertAsync(log);
                await _unitOfWork.CommitAsync();
                _logger.LogDebug("Notification log created for {Recipient}", log.RecipientAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notification log");
            }
        }

        public async Task<IPaginate<NotificationResponse>> GetNotificationLogsAsync(NotificationRequest request)
        {
            var repo = _unitOfWork.GetRepository<NotificationLog>();
            Expression<Func<NotificationLog, bool>> predicate = l =>
                (string.IsNullOrEmpty(request.DocumentId) || l.DocumentId == request.DocumentId) &&
                (string.IsNullOrEmpty(request.NotificationType) || l.NotificationType.ToString() == request.NotificationType) &&
                (string.IsNullOrEmpty(request.Recipient) ||
                 (!string.IsNullOrEmpty(l.RecipientAddress) && l.RecipientAddress.Contains(request.Recipient)));

            var logs = await repo.GetPagingListAsync(
                selector: l => _mapper.Map<NotificationResponse>(l),
                predicate: predicate,
                filter: null,
                page: request.Page,
                size: Math.Min(request.Size, ApiConstants.MAX_PAGE_SIZE),
                sortBy: request.SortBy,
                isAsc: request.IsAsc
            );

            return logs;
        }

        public async Task CleanUpOldLogsAsync()
        {
            try
            {
                // Get fresh runtime config
                var config = await _configService.GetNotificationConfigAsync();
                var retentionDays = config.LogRetentionDays;
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var logsToDelete = await logRepo.GetListAsync(predicate: l => l.CreateAt < cutoffDate);

                if (logsToDelete.Any())
                {
                    logRepo.DeleteRangeAsync(logsToDelete);
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Cleaned up {Count} old notification logs older than {Days} days (runtime config)",
                        logsToDelete.Count, retentionDays);
                }
                else
                {
                    _logger.LogInformation("No old notification logs found for cleanup (retention: {Days} days, runtime config)", retentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification log cleanup");
            }
        }

        // ✅ NEW: Additional cleanup methods for specific log types
        public async Task CleanUpProcessingLogsAsync()
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = DateTime.UtcNow.AddHours(-24);

                var processingLogs = await logRepo.GetListAsync(predicate: l =>
                    l.Subject == "PROCESSING..." &&
                    l.IsSent == false &&
                    l.CreateAt < cutoffTime);

                if (processingLogs.Any())
                {
                    logRepo.DeleteRangeAsync(processingLogs);
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Cleaned up {Count} stale processing logs", processingLogs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processing logs cleanup");
            }
        }

        public async Task CleanUpGroupedNotificationLogsAsync(int? customRetentionDays = null)
        {
            try
            {
                var retentionDays = customRetentionDays ?? (await _configService.GetNotificationConfigAsync()).LogRetentionDays;
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var groupedLogs = await logRepo.GetListAsync(predicate: l =>
                    l.DocumentId == "DAILY_GROUP" &&
                    l.CreateAt < cutoffDate);

                if (groupedLogs.Any())
                {
                    logRepo.DeleteRangeAsync(groupedLogs);
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Cleaned up {Count} old grouped notification logs using {Days} days retention (runtime config)",
                        groupedLogs.Count, retentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during grouped notification logs cleanup");
            }
        }

        // ✅ NEW: Get statistics for monitoring
        public async Task<object> GetNotificationStatisticsAsync(int days = 30)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffDate = DateTime.UtcNow.AddDays(-days);

                var logs = await logRepo.GetListAsync(predicate: l => l.CreateAt >= cutoffDate);

                var stats = new
                {
                    totalNotifications = logs.Count,
                    successfulNotifications = logs.Count(l => l.IsSent == true),
                    failedNotifications = logs.Count(l => l.IsSent == false && l.Subject != "PROCESSING..."),
                    processingNotifications = logs.Count(l => l.Subject == "PROCESSING..."),
                    expiredDocumentNotifications = logs.Count(l => l.NotificationType == Domain.Enums.NotificationType.Expired),
                    nearExpiredDocumentNotifications = logs.Count(l => l.NotificationType == Domain.Enums.NotificationType.NearingExpiration),
                    dailyGroupedNotifications = logs.Count(l => l.DocumentId == "DAILY_GROUP"),
                    systemNotifications = logs.Count(l => l.RecipientAddress == "system"),
                    uniqueRecipients = logs.Where(l => !string.IsNullOrEmpty(l.RecipientAddress) && l.RecipientAddress != "system")
                                          .Select(l => l.RecipientAddress)
                                          .Distinct()
                                          .Count(),
                    periodDays = days,
                    generatedAt = DateTime.UtcNow
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification statistics");
                return new { error = "Failed to get notification statistics" };
            }
        }

        // ✅ NEW: Get notification logs by type
        public async Task<IPaginate<NotificationResponse>> GetNotificationLogsByTypeAsync(
            string notificationType, int page = 1, int size = 20)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();

                Expression<Func<NotificationLog, bool>> predicate = l =>
                    l.NotificationType.ToString() == notificationType;

                var logs = await repo.GetPagingListAsync(
                    selector: l => _mapper.Map<NotificationResponse>(l),
                    predicate: predicate,
                    filter: null,
                    page: page,
                    size: Math.Min(size, ApiConstants.MAX_PAGE_SIZE),
                    sortBy: "CreateAt",
                    isAsc: false
                );

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification logs by type {Type}", notificationType);
                throw;
            }
        }

        // ✅ NEW: Get recent failed notifications for monitoring
        public async Task<List<NotificationResponse>> GetRecentFailedNotificationsAsync(int hours = 24)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<NotificationLog>();
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);

                var failedLogs = await repo.GetListAsync(predicate: l =>
                    l.IsSent == false &&
                    l.Subject != "PROCESSING..." &&
                    l.CreateAt >= cutoffTime);

                return failedLogs.Select(l => _mapper.Map<NotificationResponse>(l))
                                .OrderByDescending(l => l.CreateAt)
                                .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent failed notifications");
                return new List<NotificationResponse>();
            }
        }
    }
}
