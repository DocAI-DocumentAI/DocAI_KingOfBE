using AI.API.Services.Interface;

namespace AI.API.Background
{
    /// <summary>
    /// Background service that periodically cleans up old metrics and logs
    /// </summary>
    public class MetricsCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MetricsCleanupService> _logger;
        private readonly TimeSpan _interval;
        private readonly int _retentionDays;

        public MetricsCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<MetricsCleanupService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _interval = TimeSpan.FromHours(configuration.GetValue("AI:CleanupIntervalHours", 24));
            _retentionDays = configuration.GetValue("AI:MetricsRetentionDays", 90);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Metrics cleanup service started. Interval: {Interval}, Retention: {RetentionDays} days",
                _interval, _retentionDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, stoppingToken);
                    await CleanupMetricsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Metrics cleanup service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during metrics cleanup");
                }
            }
        }

        private async Task CleanupMetricsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting metrics cleanup process");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

                await metricsService.CleanupOldMetricsAsync(_retentionDays);

                _logger.LogInformation("Metrics cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old metrics");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Metrics cleanup service is stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
