using AutoMapper;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;
using System.Linq.Expressions;

namespace Notification.API.Services.Implement
{
    public class NotificationLogService : INotificationLogService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<NotificationLogService> _logger;
        private readonly IMapper _mapper;
        public NotificationLogService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<NotificationLogService> logger,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task CreateLogAsync(NotificationLog log)
        {
            try
            {
                _logger.LogInformation("Attempting to insert log for recipient: {Recipient}", log.RecipientAddress);
                await _unitOfWork.GetRepository<NotificationLog>().InsertAsync(log);

                _logger.LogInformation("Attempting to commit log to database...");
                var result = await _unitOfWork.CommitAsync();

                if (result > 0)
                {
                    _logger.LogInformation("[SUCCESS] Committed successfully. {Count} records saved.", result);
                }
                else
                {
                    _logger.LogWarning("[FAIL] Commit executed but no records were saved. There might be an issue with the DbContext tracking.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FATAL] Failed to commit notification log to database for recipient: {Recipient}", log.RecipientAddress);
            }
        }

        public async Task<IPaginate<NotificationResponse>> GetNotificationLogsAsync(NotificationRequest request)
        {
            var repo = _unitOfWork.GetRepository<NotificationLog>();

            // Build a dynamic predicate for filtering
            Expression<Func<NotificationLog, bool>> predicate = l =>
                (!request.DocumentId.HasValue || l.DocumentId == request.DocumentId.Value) &&
                (string.IsNullOrEmpty(request.NotificationType) || l.NotificationType.ToString() == request.NotificationType) &&
                (string.IsNullOrEmpty(request.Recipient) || (l.RecipientAddress != null && l.RecipientAddress.Contains(request.Recipient)));

            var logs = await repo.GetPagingListAsync(
                selector: l => _mapper.Map<NotificationResponse>(l),
                filter: null,
                predicate: predicate,
                orderBy: null, // We use sortBy and isAsc instead
                page: request.Page,
                size: request.Size,
                sortBy: request.SortBy,
                isAsc: request.IsAsc
            );

            return logs;
        }

        public async Task CleanUpOldLogsAsync()
        {
            var configRepo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await configRepo.SingleOrDefaultAsync(predicate: p => p.ConfigKey == "Default");

            // Default to 90 days if config is missing
            var retentionDays = config?.LogRetentionDays ?? 90;

            _logger.LogInformation("Cleaning up notification logs older than {RetentionDays} days.", retentionDays);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var logRepo = _unitOfWork.GetRepository<NotificationLog>();
            var logsToDelete = await logRepo.GetListAsync(predicate: l => l.CreateAt < cutoffDate);

            if (logsToDelete.Any())
            {
                logRepo.DeleteRangeAsync(logsToDelete);
                var deletedCount = await _unitOfWork.CommitAsync();
                _logger.LogInformation("Successfully deleted {Count} old notification logs.", deletedCount);
            }
            else
            {
                _logger.LogInformation("No old notification logs to delete.");
            }
        }
    }
}
