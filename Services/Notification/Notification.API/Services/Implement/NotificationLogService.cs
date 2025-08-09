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

        public NotificationLogService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            ILogger<NotificationLogService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
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
                (!request.DocumentId.IsNullOrWhiteSpace() || l.DocumentId == request.DocumentId) &&
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
                var configRepo = _unitOfWork.GetRepository<NotificationConfig>();
                var config = await configRepo.SingleOrDefaultAsync(predicate: c => c.ConfigKey == ApiConstants.DEFAULT_CONFIG_KEY);

                var retentionDays = config?.LogRetentionDays ?? 90;
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var logsToDelete = await logRepo.GetListAsync(predicate: l => l.CreateAt < cutoffDate);

                if (logsToDelete.Any())
                {
                    logRepo.DeleteRangeAsync(logsToDelete);
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Cleaned up {Count} old notification logs", logsToDelete.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification log cleanup");
            }
        }
    }
}
