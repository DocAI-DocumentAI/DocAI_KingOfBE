using System.Linq.Expressions;
using AutoMapper;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;

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
    }
}
