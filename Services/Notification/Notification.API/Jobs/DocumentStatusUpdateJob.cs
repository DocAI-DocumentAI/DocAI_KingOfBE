using Microsoft.Extensions.Logging;
using Notification.API.Services.Interfaces;
using Quartz;
using Shared.Utils;

namespace Notification.API.Jobs
{
    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public class DocumentStatusUpdateJob : IJob
    {
        private readonly ILogger<DocumentStatusUpdateJob> _logger;
        private readonly IDocumentScanService _scanService;
        private readonly IRedisService _redisService;

        public DocumentStatusUpdateJob(
            ILogger<DocumentStatusUpdateJob> logger,
            IDocumentScanService scanService,
            IRedisService redisService)
        {
            _logger = logger;
            _scanService = scanService;
            _redisService = redisService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = Guid.NewGuid().ToString("N")[..8];
            var jobType = "document_status_update";
            var lockDuration = TimeSpan.FromMinutes(40);

            // Try lock job
            var jobLocked = await _redisService.TryLockJobAsync(jobType, lockDuration);

            if (!jobLocked)
            {
                _logger.LogWarning("Another status update job is running, skipping - JobId: {JobId}", jobId);
                return;
            }

            var vietnamNow = TimeZoneHelper.VietnamNow;
            _logger.LogInformation("DocumentStatusUpdateJob started at Vietnam time: {VietnamTime} - JobId: {JobId}",
                vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"), jobId);

            try
            {
                var documentsToUpdate = await _scanService.GetDocumentsForStatusUpdateAsync();

                if (!documentsToUpdate.Any())
                {
                    _logger.LogInformation("No documents found requiring status update - JobId: {JobId}", jobId);
                    return;
                }

                _logger.LogInformation("Found {Count} documents requiring status update to Archived - JobId: {JobId}",
                    documentsToUpdate.Count, jobId);

                await _scanService.ProcessDocumentStatusUpdatesAsync(documentsToUpdate, jobId);

                _logger.LogInformation("DocumentStatusUpdateJob completed successfully - JobId: {JobId}", jobId);
                context.JobDetail.JobDataMap.Put("LastSuccessfulRun", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DocumentStatusUpdateJob - JobId: {JobId}", jobId);
                context.JobDetail.JobDataMap.Put("LastErrorTime", DateTime.UtcNow);
                context.JobDetail.JobDataMap.Put("LastError", ex.Message);
                throw new JobExecutionException(ex, refireImmediately: false);
            }
            finally
            {
                // Always release lock
                await _redisService.ReleaseLockJobAsync(jobType);
            }
        }
    }
}
