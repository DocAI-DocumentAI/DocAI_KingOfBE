using AutoMapper;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz;

namespace Notification.API.Services.Implement
{
    public class NotificationConfigService : INotificationConfigService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<NotificationConfigService> _logger;
        private readonly IMapper _mapper;
        private readonly INotificationSchedulerService _schedulerService;
        public NotificationConfigService(
               IUnitOfWork<NotificationDbContext> unitOfWork,
               ILogger<NotificationConfigService> logger,
               IMapper mapper,
               INotificationSchedulerService schedulerService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _schedulerService = schedulerService;
        }

        public async Task<NotificationConfigResponse> GetNotificationConfigAsync()
        {
            var config = await _unitOfWork.GetRepository<NotificationConfig>().SingleOrDefaultAsync(predicate: p => p.ConfigKey == "Default");
            if (config == null)
            {
                _logger.LogError("Default notification configuration not found in the database.");
                throw new InvalidOperationException("Default notification configuration not found.");
            }
            return _mapper.Map<NotificationConfigResponse>(config);
        }

        public async Task<NotificationConfigResponse> UpdateNotificationConfigAsync(NotificationConfigRequest request)
        {
            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await repo.SingleOrDefaultAsync(predicate: p => p.ConfigKey == "Default");
            if (config == null)
            {
                throw new InvalidOperationException("Default notification configuration not found.");
            }

            // Validate Cron Expression before saving
            if (!CronExpression.IsValidExpression(request.ScanCronExpression))
            {
                throw new BadHttpRequestException($"Invalid cron expression: {request.ScanCronExpression}");
            }

            bool scheduleNeedsUpdate = config.ScanCronExpression != request.ScanCronExpression;

            _mapper.Map(request, config);
            config.UpdateAt = DateTime.UtcNow;

            repo.UpdateAsync(config);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Notification configuration updated successfully.");

            // Reschedule the job if the cron expression has changed
            if (scheduleNeedsUpdate)
            {
                await _schedulerService.UpdateDocumentScanJobSchedule(request.ScanCronExpression);
            }

            return _mapper.Map<NotificationConfigResponse>(config);
        }
    }
}
