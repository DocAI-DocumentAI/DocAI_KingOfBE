using Notification.API.Jobs;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Services.Implement
{
    public class NotificationSchedulerService : INotificationSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<NotificationSchedulerService> _logger;
        private readonly TriggerKey _triggerKey = new($"{nameof(NotificationScanJob)}-trigger");

        public NotificationSchedulerService(
               ISchedulerFactory schedulerFactory,
               ILogger<NotificationSchedulerService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        public async Task UpdateDocumentScanJobSchedule(string newCronExpression)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var existingTrigger = await scheduler.GetTrigger(_triggerKey);

            if (existingTrigger == null)
            {
                _logger.LogError("Could not find trigger with key: {TriggerKey}. Cannot update schedule.", _triggerKey);
                return;
            }

            var newTrigger = TriggerBuilder.Create()
                .ForJob(existingTrigger.JobKey)
                .WithIdentity(_triggerKey)
                .WithCronSchedule(newCronExpression)
                .Build();

            await scheduler.RescheduleJob(_triggerKey, newTrigger);
            _logger.LogInformation("Successfully rescheduled job with new cron expression: {CronExpression}", newCronExpression);
        }
    }

}
