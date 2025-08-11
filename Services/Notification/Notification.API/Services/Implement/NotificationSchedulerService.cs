using Notification.API.Jobs;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Services.Implement
{
    public class NotificationSchedulerService : INotificationSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<NotificationSchedulerService> _logger;

        public NotificationSchedulerService(
            ISchedulerFactory schedulerFactory,
            ILogger<NotificationSchedulerService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        public async Task UpdateDocumentScanJobSchedule(string cronExpression)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var triggerKey = new TriggerKey("NotificationScanTrigger");

                if (await scheduler.CheckExists(triggerKey))
                {
                    var newTrigger = TriggerBuilder.Create()
                        .WithIdentity(triggerKey)
                        .WithCronSchedule(cronExpression)
                        .Build();

                    await scheduler.RescheduleJob(triggerKey, newTrigger);
                    _logger.LogInformation("Updated notification scan job schedule to: {CronExpression}", cronExpression);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification scan job schedule");
            }
        }
    }

}
