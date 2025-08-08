using MassTransit;
using Notification.API.Command;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Jobs
{
    [DisallowConcurrentExecution]
    public class NotificationScanJob : IJob
    {
        private readonly ILogger<NotificationScanJob> _logger;
        private readonly IDocumentScanService _scanService;

        public NotificationScanJob(ILogger<NotificationScanJob> logger, IDocumentScanService scanService)
        {
            _logger = logger;
            _scanService = scanService;
        }


        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                _logger.LogInformation("NotificationScanJob started");
                await _scanService.ScanAndProcessDocumentsAsync();
                _logger.LogInformation("NotificationScanJob completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception occurred in the document scan job.");
            }
        }
    }
}
