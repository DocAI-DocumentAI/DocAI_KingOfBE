using Notification.API.Jobs;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Services.Implement
{
    public class NotificationSchedulerService : INotificationSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<NotificationSchedulerService> _logger;

        // ✅ ADDED: Vietnam timezone
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

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
                if (!CronExpression.IsValidExpression(cronExpression))
                {
                    throw new ArgumentException($"Invalid cron expression: {cronExpression}");
                }

                var scheduler = await _schedulerFactory.GetScheduler();
                var triggerKey = new TriggerKey("NotificationScanTrigger");

                if (await scheduler.CheckExists(triggerKey))
                {
                    // ✅ ENHANCED: Create new trigger with Vietnam timezone
                    var newTrigger = TriggerBuilder.Create()
                        .WithIdentity(triggerKey)
                        .WithCronSchedule(cronExpression, x => x.InTimeZone(VietnamTimeZone)) // ✅ Vietnam timezone
                        .Build();

                    await scheduler.RescheduleJob(triggerKey, newTrigger);

                    _logger.LogInformation("✅ Updated notification scan job schedule to: '{CronExpression}' (Vietnam timezone)",
                        cronExpression);
                }
                else
                {
                    _logger.LogWarning("⚠️ NotificationScanTrigger not found - cannot update schedule");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating notification scan job schedule to '{CronExpression}'",
                    cronExpression);
                throw;
            }
        }

        // ✅ ADDED: Pause all notification jobs
        public async Task PauseAllJobs()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                // Pause specific notification jobs
                var scanJobKey = new JobKey("NotificationScanJob");
                var cleanupJobKey = new JobKey("CleanUpOldLogsJob");

                if (await scheduler.CheckExists(scanJobKey))
                {
                    await scheduler.PauseJob(scanJobKey);
                    _logger.LogInformation("⏸️ Paused NotificationScanJob");
                }

                if (await scheduler.CheckExists(cleanupJobKey))
                {
                    await scheduler.PauseJob(cleanupJobKey);
                    _logger.LogInformation("⏸️ Paused CleanUpOldLogsJob");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error pausing notification jobs");
                throw;
            }
        }

        // ✅ ADDED: Resume all notification jobs
        public async Task ResumeAllJobs()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var scanJobKey = new JobKey("NotificationScanJob");
                var cleanupJobKey = new JobKey("CleanUpOldLogsJob");

                if (await scheduler.CheckExists(scanJobKey))
                {
                    await scheduler.ResumeJob(scanJobKey);
                    _logger.LogInformation("▶️ Resumed NotificationScanJob");
                }

                if (await scheduler.CheckExists(cleanupJobKey))
                {
                    await scheduler.ResumeJob(cleanupJobKey);
                    _logger.LogInformation("▶️ Resumed CleanUpOldLogsJob");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error resuming notification jobs");
                throw;
            }
        }

        // ✅ ADDED: Get scheduler status
        public async Task<object> GetSchedulerStatusAsync()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var scanJobKey = new JobKey("NotificationScanJob");
                var cleanupJobKey = new JobKey("CleanUpOldLogsJob");
                var scanTriggerKey = new TriggerKey("NotificationScanTrigger");
                var cleanupTriggerKey = new TriggerKey("CleanUpOldLogsTrigger");

                var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

                return new
                {
                    SchedulerStarted = scheduler.IsStarted,
                    SchedulerInStandbyMode = scheduler.InStandbyMode,
                    CurrentVietnamTime = vietnamNow.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                    Jobs = new
                    {
                        ScanJob = await GetJobStatus(scheduler, scanJobKey, scanTriggerKey),
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

        // ✅ ADDED: Get individual job status
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
                    // Convert to Vietnam time
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

        // ✅ ADDED: Update cleanup job schedule
        public async Task UpdateCleanupJobSchedule(string cronExpression)
        {
            try
            {
                if (!CronExpression.IsValidExpression(cronExpression))
                {
                    throw new ArgumentException($"Invalid cron expression: {cronExpression}");
                }

                var scheduler = await _schedulerFactory.GetScheduler();
                var triggerKey = new TriggerKey("CleanUpOldLogsTrigger");

                if (await scheduler.CheckExists(triggerKey))
                {
                    var newTrigger = TriggerBuilder.Create()
                        .WithIdentity(triggerKey)
                        .WithCronSchedule(cronExpression, x => x.InTimeZone(VietnamTimeZone))
                        .Build();

                    await scheduler.RescheduleJob(triggerKey, newTrigger);

                    _logger.LogInformation("✅ Updated cleanup job schedule to: '{CronExpression}' (Vietnam timezone)",
                        cronExpression);
                }
                else
                {
                    _logger.LogWarning("⚠️ CleanUpOldLogsTrigger not found - cannot update schedule");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating cleanup job schedule to '{CronExpression}'", cronExpression);
                throw;
            }
        }

        // ✅ ADDED: Trigger manual job execution
        public async Task TriggerScanJobNow()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey("NotificationScanJob");

                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.TriggerJob(jobKey);
                    _logger.LogInformation("🚀 Manually triggered NotificationScanJob");
                }
                else
                {
                    throw new InvalidOperationException("NotificationScanJob not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error triggering scan job manually");
                throw;
            }
        }

        // ✅ ADDED: Trigger manual cleanup
        public async Task TriggerCleanupJobNow()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey("CleanUpOldLogsJob");

                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.TriggerJob(jobKey);
                    _logger.LogInformation("🚀 Manually triggered CleanUpOldLogsJob");
                }
                else
                {
                    throw new InvalidOperationException("CleanUpOldLogsJob not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error triggering cleanup job manually");
                throw;
            }
        }
    }

}
