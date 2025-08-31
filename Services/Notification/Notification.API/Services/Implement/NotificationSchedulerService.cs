using Notification.API.Jobs;
using Notification.API.Services.Interfaces;
using Quartz;
using Shared.Utils;

namespace Notification.API.Services.Implement
{
    public class NotificationSchedulerService : INotificationSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<NotificationSchedulerService> _logger;

        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public NotificationSchedulerService(
            ISchedulerFactory schedulerFactory,
            ILogger<NotificationSchedulerService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        public async Task UpdateExpiredDocumentJobSchedule(string cronExpression)
        {
            await UpdateJobSchedule("ExpiredDocumentTrigger", cronExpression, "Expired Document");
        }
        public async Task UpdateNearExpiredDocumentJobSchedule(string cronExpression)
        {
            await UpdateJobSchedule("NearExpiredDocumentTrigger", cronExpression, "Near-Expired Document");

        }
        public async Task UpdateDocumentStatusUpdateJobSchedule(string cronExpression)
        {
            await UpdateJobSchedule("DocumentStatusUpdateTrigger", cronExpression, "Document Status Update");
        }

        public async Task TriggerDocumentStatusUpdateJobNow()
        {
            await TriggerJobNow("DocumentStatusUpdateJob", "Document Status Update");
        }
        private async Task UpdateJobSchedule(string triggerKey, string cronExpression, string jobName)
        {
            try
            {
                if (!CronExpression.IsValidExpression(cronExpression))
                {
                    throw new ArgumentException($"Invalid cron expression: {cronExpression}");
                }

                var scheduler = await _schedulerFactory.GetScheduler();
                var trigger = new TriggerKey(triggerKey);

                if (await scheduler.CheckExists(trigger))
                {
                    var newTrigger = TriggerBuilder.Create()
                        .WithIdentity(trigger)
                        .WithCronSchedule(cronExpression, x => x.InTimeZone(TimeZoneHelper.VietnamTimeZone))
                        .Build();

                    await scheduler.RescheduleJob(trigger, newTrigger);

                    _logger.LogInformation("✅ Updated {JobName} schedule to: '{CronExpression}' (Vietnam timezone)",
                        jobName, cronExpression);
                }
                else
                {
                    _logger.LogWarning("⚠️ {TriggerKey} not found - cannot update schedule", triggerKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating {JobName} schedule to '{CronExpression}'",
                    jobName, cronExpression);
                throw;
            }
        }

        public async Task PauseAllJobs()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var jobKeys = new[]
                {
                    new JobKey("ExpiredDocumentNotificationJob"),
                    new JobKey("NearExpiredDocumentNotificationJob"),
                    new JobKey("CleanUpOldLogsJob"),
                    new JobKey("DocumentStatusUpdateJob")
                };

                foreach (var jobKey in jobKeys)
                {
                    if (await scheduler.CheckExists(jobKey))
                    {
                        await scheduler.PauseJob(jobKey);
                        _logger.LogInformation("⏸️ Paused {JobName}", jobKey.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error pausing notification jobs");
                throw;
            }
        }

        public async Task ResumeAllJobs()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var jobKeys = new[]
                {
                    new JobKey("ExpiredDocumentNotificationJob"),
                    new JobKey("NearExpiredDocumentNotificationJob"),
                    new JobKey("CleanUpOldLogsJob"),
                    new JobKey("DocumentStatusUpdateJob")
                };

                foreach (var jobKey in jobKeys)
                {
                    if (await scheduler.CheckExists(jobKey))
                    {
                        await scheduler.ResumeJob(jobKey);
                        _logger.LogInformation("▶️ Resumed {JobName}", jobKey.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error resuming notification jobs");
                throw;
            }
        }

        public async Task<object> GetSchedulerStatusAsync()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var expiredJobKey = new JobKey("ExpiredDocumentNotificationJob");
                var nearExpiredJobKey = new JobKey("NearExpiredDocumentNotificationJob");
                var statusUpdateJobKey = new JobKey("DocumentStatusUpdateJob");
                var cleanupJobKey = new JobKey("CleanUpOldLogsJob");

                var expiredTriggerKey = new TriggerKey("ExpiredDocumentTrigger");
                var nearExpiredTriggerKey = new TriggerKey("NearExpiredDocumentTrigger");
                var statusUpdateTriggerKey = new TriggerKey("DocumentStatusUpdateTrigger");
                var cleanupTriggerKey = new TriggerKey("CleanUpOldLogsTrigger");

                var vietnamNow = TimeZoneHelper.VietnamNow;

                return new
                {
                    SchedulerStarted = scheduler.IsStarted,
                    SchedulerInStandbyMode = scheduler.InStandbyMode,
                    CurrentVietnamTime = vietnamNow.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                    Jobs = new
                    {
                        ExpiredDocumentJob = await GetJobStatus(scheduler, expiredJobKey, expiredTriggerKey),
                        NearExpiredDocumentJob = await GetJobStatus(scheduler, nearExpiredJobKey, nearExpiredTriggerKey),
                        DocumentStatusUpdateJob = await GetJobStatus(scheduler, statusUpdateJobKey, statusUpdateTriggerKey),
                        CleanupJob = await GetJobStatus(scheduler, cleanupJobKey, cleanupTriggerKey)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduler status");
                return new { Error = ex.Message };
            }
        }

        private async Task<object> GetJobStatus(IScheduler scheduler, JobKey jobKey, TriggerKey triggerKey)
        {
            try
            {
                var jobExists = await scheduler.CheckExists(jobKey);
                var triggerExists = await scheduler.CheckExists(triggerKey);

                if (!jobExists || !triggerExists)
                {
                    return new { Status = "Not Found", JobExists = jobExists, TriggerExists = triggerExists };
                }

                var trigger = await scheduler.GetTrigger(triggerKey);
                var triggerState = await scheduler.GetTriggerState(triggerKey);

                DateTime? nextFireTime = null;
                DateTime? previousFireTime = null;

                if (trigger != null)
                {
                    if (trigger.GetNextFireTimeUtc().HasValue)
                    {
                        nextFireTime = TimeZoneInfo.ConvertTimeFromUtc(
                            trigger.GetNextFireTimeUtc().Value.DateTime, VietnamTimeZone);
                    }

                    if (trigger.GetPreviousFireTimeUtc().HasValue)
                    {
                        previousFireTime = TimeZoneInfo.ConvertTimeFromUtc(
                            trigger.GetPreviousFireTimeUtc().Value.DateTime, VietnamTimeZone);
                    }
                }

                return new
                {
                    Status = triggerState.ToString(),
                    NextFireTime = nextFireTime?.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                    PreviousFireTime = previousFireTime?.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                    CronExpression = (trigger as ICronTrigger)?.CronExpressionString,
                    TimeZone = "SE Asia Standard Time (Vietnam)"
                };
            }
            catch (Exception ex)
            {
                return new { Status = "Error", Error = ex.Message };
            }
        }

        public async Task TriggerExpiredDocumentJobNow()
        {
            await TriggerJobNow("ExpiredDocumentNotificationJob", "Expired Document Notification");
        }

        public async Task TriggerNearExpiredDocumentJobNow()
        {
            await TriggerJobNow("NearExpiredDocumentNotificationJob", "Near-Expired Document Notification");
        }

        public async Task TriggerCleanupJobNow()
        {
            await TriggerJobNow("CleanUpOldLogsJob", "Cleanup Old Logs");
        }

        private async Task TriggerJobNow(string jobName, string displayName)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey(jobName);

                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.TriggerJob(jobKey);
                    _logger.LogInformation("🚀 Manually triggered {DisplayName} job", displayName);
                }
                else
                {
                    throw new InvalidOperationException($"{displayName} job not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error triggering {DisplayName} job manually", displayName);
                throw;
            }
        }
    }
}