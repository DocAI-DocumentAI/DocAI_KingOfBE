using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class ConfigSyncService  : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfigSyncService> _logger;
        private string _lastConfigHash;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
        private NotificationConfigResponse _previousConfig;

        public ConfigSyncService(IServiceProvider serviceProvider, ILogger<ConfigSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await PerformConfigSyncAsync("Initial startup sync");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                    await PerformConfigSyncAsync("Periodic sync check");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ConfigSyncService shutting down gracefully");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during periodic config sync");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private async Task PerformConfigSyncAsync(string reason)
        {
            if (!await _syncSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                _logger.LogDebug("Config sync already in progress, skipping");
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<INotificationConfigService>();
                var schedulerService = scope.ServiceProvider.GetService<INotificationSchedulerService>();

                if (schedulerService == null)
                {
                    _logger.LogWarning("Scheduler service not available, skipping config sync");
                    return;
                }

                var config = await configService.GetNotificationConfigAsync();
                var currentConfigHash = ComputeConfigHash(config);

                if (currentConfigHash == _lastConfigHash)
                {
                    _logger.LogDebug("Config unchanged, skipping sync - {Reason}", reason);
                    return;
                }

                _logger.LogInformation("Config changes detected, syncing schedules - {Reason}", reason);

                var quartzStatus = await schedulerService.GetSchedulerStatusAsync();
                var needsUpdate = await CheckIfUpdateNeeded(config, quartzStatus);

                if (needsUpdate.expiredNeedsUpdate)
                {
                    await schedulerService.UpdateExpiredDocumentJobSchedule(config.ExpiredNotificationCron);
                    _logger.LogInformation("Updated expired document schedule to: '{Cron}'",
                        config.ExpiredNotificationCron);
                }

                if (needsUpdate.nearExpiredNeedsUpdate)
                {
                    await schedulerService.UpdateNearExpiredDocumentJobSchedule(config.NearExpiredNotificationCron);
                    _logger.LogInformation("Updated near-expired document schedule to: '{Cron}'",
                        config.NearExpiredNotificationCron);
                }

                if (needsUpdate.enabledStateChanged)
                {
                    if (config.QuartzEnabled)
                    {
                        await schedulerService.ResumeAllJobs();
                        _logger.LogInformation("Resumed all jobs - quartzEnabled: true");
                    }
                    else
                    {
                        await schedulerService.PauseAllJobs();
                        _logger.LogInformation("Paused all jobs - quartzEnabled: false");
                    }
                }

                // Handle LogRetentionDays changes
                if (_previousConfig != null && config.LogRetentionDays < _previousConfig.LogRetentionDays)
                {
                    _logger.LogInformation("Log retention decreased from {Old} to {New} days - triggering immediate cleanup",
                        _previousConfig.LogRetentionDays, config.LogRetentionDays);

                    var logService = scope.ServiceProvider.GetService<INotificationLogService>();
                    if (logService != null)
                    {
                        // Trigger immediate cleanup in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await logService.CleanUpOldLogsAsync();
                                await logService.CleanUpGroupedNotificationLogsAsync();
                                _logger.LogInformation("Completed immediate log cleanup due to retention decrease");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during immediate log cleanup");
                            }
                        });
                    }
                }

                _previousConfig = config;
                _lastConfigHash = currentConfigHash;
                _logger.LogInformation("Config sync completed successfully - {Reason}", reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync config with Quartz schedules - {Reason}", reason);
                throw;
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        // Include ALL config values in hash
        private string ComputeConfigHash(NotificationConfigResponse config)
        {
            var configString = $"{config.ExpiredNotificationCron}|{config.NearExpiredNotificationCron}|" +
                              $"{config.QuartzEnabled}|{config.EnableExpiredNotifications}|{config.EnableNearExpiredNotifications}|" +
                              $"{config.WarningThresholdDays}|{config.LogRetentionDays}|{config.UpdateAt}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(configString));
            return Convert.ToBase64String(hashBytes);
        }

        private async Task<(bool expiredNeedsUpdate, bool nearExpiredNeedsUpdate, bool enabledStateChanged)>
            CheckIfUpdateNeeded(NotificationConfigResponse config, dynamic quartzStatus)
        {
            try
            {
                string currentExpiredCron = null;
                string currentNearExpiredCron = null;
                bool currentQuartzRunning = false;

                if (quartzStatus != null && quartzStatus.GetType().GetProperty("Jobs") != null)
                {
                    var jobs = quartzStatus.Jobs;
                    if (jobs != null)
                    {
                        var jobsType = jobs.GetType();

                        var expiredJob = jobsType.GetProperty("ExpiredDocumentJob")?.GetValue(jobs);
                        if (expiredJob != null)
                        {
                            currentExpiredCron = expiredJob.GetType().GetProperty("CronExpression")?.GetValue(expiredJob)?.ToString();
                        }

                        var nearExpiredJob = jobsType.GetProperty("NearExpiredDocumentJob")?.GetValue(jobs);
                        if (nearExpiredJob != null)
                        {
                            currentNearExpiredCron = nearExpiredJob.GetType().GetProperty("CronExpression")?.GetValue(nearExpiredJob)?.ToString();
                        }
                    }

                    var schedulerStarted = quartzStatus.GetType().GetProperty("SchedulerStarted")?.GetValue(quartzStatus);
                    var standbyMode = quartzStatus.GetType().GetProperty("SchedulerInStandbyMode")?.GetValue(quartzStatus);
                    currentQuartzRunning = schedulerStarted is true && standbyMode is false;
                }

                bool expiredNeedsUpdate = !string.Equals(currentExpiredCron, config.ExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool nearExpiredNeedsUpdate = !string.Equals(currentNearExpiredCron, config.NearExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool enabledStateChanged = currentQuartzRunning != config.QuartzEnabled;

                return (expiredNeedsUpdate, nearExpiredNeedsUpdate, enabledStateChanged);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if update needed, assuming update is required");
                return (true, true, true);
            }
        }

        public override void Dispose()
        {
            _syncSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
