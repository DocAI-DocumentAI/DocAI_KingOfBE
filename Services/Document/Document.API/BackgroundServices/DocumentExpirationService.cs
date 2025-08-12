using Document.API.Services.Interfaces;

namespace Document.API.BackgroundServices
{
    /// <summary>
    /// Background service that automatically:
    /// 1. Rejects documents that have been pending for more than 7 days (BR-214)
    /// 2. Releases claims that have been inactive for more than 30 minutes
    /// </summary>
    public class DocumentExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public DocumentExpirationService(
            IServiceProvider serviceProvider,
            ILogger<DocumentExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document Expiration Service started. Will check for expired documents every {Interval}", _checkInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredDocuments();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired documents");
                }

                // Wait for the next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessExpiredDocuments()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();

                _logger.LogInformation("Starting expired document processing check");

                // Process expired submissions (7-day auto-rejection)
                await approvalService.ProcessExpiredSubmissionsAsync();

                // Process inactive claims (30-minute auto-release)
                await approvalService.ProcessInactiveClaimsAsync();

                _logger.LogInformation("Completed expired document and inactive claims processing check");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process expired documents and inactive claims");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document Expiration Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
