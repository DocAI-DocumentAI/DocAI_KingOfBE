using AI.API.Services.Interface;

namespace AI.API
{
    public class MetricsCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MetricsCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24); // Daily

        public MetricsCleanupService(
            IServiceProvider serviceProvider,
            ILogger<MetricsCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics Cleanup Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                    var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

                    var autoCleanupEnabled = await configService.GetConfigurationAsync("AI:EnableAutoCleanup", false);
                    if (autoCleanupEnabled)
                    {
                        var retentionDays = await configService.GetConfigurationAsync("AI:MetricsRetentionDays", 90);

                        _logger.LogInformation("Starting metrics cleanup for data older than {Days} days", retentionDays);

                        var success = await metricsService.CleanupOldMetricsAsync(retentionDays);

                        if (success)
                        {
                            _logger.LogInformation("Metrics cleanup completed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Metrics cleanup completed with issues");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics cleanup service");
                }
            }

            _logger.LogInformation("Metrics Cleanup Service stopped");
        }
    }
}
