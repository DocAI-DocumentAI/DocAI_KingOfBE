using Notification.API.Jobs;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Services.Implement
{
    public class NotificationSchedulerService : INotificationSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<NotificationSchedulerService> _logger;
        private readonly TriggerKey _triggerKey = new TriggerKey("NotificationScanJob-trigger");

        public NotificationSchedulerService(
            ISchedulerFactory schedulerFactory,
            ILogger<NotificationSchedulerService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        public async Task UpdateDocumentScanJobSchedule(string newCronExpression)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                if (scheduler == null)
                {
                    _logger.LogError("Scheduler is not available");
                    return;
                }

                var existingTrigger = await scheduler.GetTrigger(_triggerKey);
                if (existingTrigger == null)
                {
                    _logger.LogWarning("Trigger not found: {TriggerKey}, cannot update schedule", _triggerKey);
                    return;
                }

                // Validate cron expression
                if (!CronExpression.IsValidExpression(newCronExpression))
                {
                    _logger.LogError("Invalid cron expression: {CronExpression}", newCronExpression);
                    return;
                }

                var newTrigger = TriggerBuilder.Create()
                    .ForJob(existingTrigger.JobKey)
                    .WithIdentity(_triggerKey)
                    .WithCronSchedule(newCronExpression)
                    .Build();

                await scheduler.RescheduleJob(_triggerKey, newTrigger);
                _logger.LogInformation("Successfully updated job schedule: {CronExpression}", newCronExpression);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document scan job schedule");
                // Don't throw - scheduler issues shouldn't break config updates
            }
        }
    }

}
