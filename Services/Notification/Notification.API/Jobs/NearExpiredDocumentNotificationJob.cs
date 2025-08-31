
using MassTransit;
using Microsoft.Extensions.Logging;
using Notification.API.Command;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Quartz;

namespace Notification.API.Jobs
{

    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public class NearExpiredDocumentNotificationJob : IJob
    {
        private readonly ILogger<NearExpiredDocumentNotificationJob> _logger;
        private readonly IDocumentScanService _scanService;
        private readonly IRedisService _redisService;

        public NearExpiredDocumentNotificationJob(
            ILogger<NearExpiredDocumentNotificationJob> logger,
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
            var jobType = "near_expired_notification";
            var lockDuration = TimeSpan.FromMinutes(25);

            // Try lock job
            var jobLocked = await _redisService.TryLockJobAsync(jobType, lockDuration);

            if (!jobLocked)
            {
                _logger.LogWarning("Another near-expired notification job is running, skipping - JobId: {JobId}", jobId);
                return;
            }

            _logger.LogInformation("NearExpiredDocumentNotificationJob started - JobId: {JobId}", jobId);

            try
            {
                var nearExpiredDocuments = await _scanService.GetNearExpiredDocumentsAsync();

                if (!nearExpiredDocuments.Any())
                {
                    _logger.LogInformation("No near-expired documents found - JobId: {JobId}", jobId);
                    return;
                }

                _logger.LogInformation("Found {Count} near-expired documents to process - JobId: {JobId}",
                    nearExpiredDocuments.Count, jobId);

                await _scanService.ProcessNearExpiredDocumentsAsync(nearExpiredDocuments, jobId);

                _logger.LogInformation("NearExpiredDocumentNotificationJob completed - JobId: {JobId}", jobId);
                context.JobDetail.JobDataMap.Put("LastSuccessfulRun", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NearExpiredDocumentNotificationJob - JobId: {JobId}", jobId);
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
