using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class ConfigSyncService  : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfigSyncService> _logger;

        public ConfigSyncService(IServiceProvider serviceProvider, ILogger<ConfigSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for application to fully start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<INotificationConfigService>();
            var schedulerService = scope.ServiceProvider.GetService<INotificationSchedulerService>();

            if (schedulerService == null)
            {
                _logger.LogWarning("Scheduler service not available, skipping config sync");
                return;
            }

            try
            {
                _logger.LogInformation("Syncing database config with Quartz schedules...");

                var config = await configService.GetNotificationConfigAsync();

                // Force update schedules with current database config
                await schedulerService.UpdateExpiredDocumentJobSchedule(config.ExpiredNotificationCron);
                await schedulerService.UpdateNearExpiredDocumentJobSchedule(config.NearExpiredNotificationCron);

                _logger.LogInformation("Config sync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync config with Quartz schedules");
            }
        }
    }
}
