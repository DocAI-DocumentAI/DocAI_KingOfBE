using MassTransit;
using Notification.API.Command;
using Notification.API.Services.Interfaces;
using Quartz;

namespace Notification.API.Jobs
{
    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution] 
    public class NotificationScanJob : IJob
    {
        private readonly ILogger<NotificationScanJob> _logger;
        private readonly IDocumentScanService _scanService;
        private static readonly SemaphoreSlim _semaphore = new(1, 1); // ✅ THÊM: Semaphore protection

        public NotificationScanJob(ILogger<NotificationScanJob> logger, IDocumentScanService scanService)
        {
            _logger = logger;
            _scanService = scanService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            // ✅ THÊM: Semaphore check
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("NotificationScanJob is already running, skipping this execution");
                return;
            }

            try
            {
                _logger.LogInformation("NotificationScanJob started");
                await _scanService.ScanAndProcessDocumentsAsync();
                _logger.LogInformation("NotificationScanJob completed successfully");

                // ✅ THÊM: Update job data
                context.JobDetail.JobDataMap.Put("LastSuccessfulRun", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception occurred in the document scan job.");

                // ✅ THÊM: Update error info
                context.JobDetail.JobDataMap.Put("LastErrorTime", DateTime.UtcNow);
                context.JobDetail.JobDataMap.Put("LastError", ex.Message);

                // ✅ THÊM: Throw để Quartz biết job failed
                throw new JobExecutionException(ex, refireImmediately: false);
            }
            finally
            {
                _semaphore.Release(); // ✅ THÊM: Release semaphore
            }
        }
    }
}