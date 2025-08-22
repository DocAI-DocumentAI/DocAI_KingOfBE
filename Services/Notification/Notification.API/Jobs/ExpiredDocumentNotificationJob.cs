
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
    public class ExpiredDocumentNotificationJob : IJob
    {
        private readonly ILogger<ExpiredDocumentNotificationJob> _logger;
        private readonly IDocumentScanService _scanService;
        private readonly IBus _bus;

        public ExpiredDocumentNotificationJob(
            ILogger<ExpiredDocumentNotificationJob> logger,
            IDocumentScanService scanService,
            IBus bus)
        {
            _logger = logger;
            _scanService = scanService;
            _bus = bus;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("ExpiredDocumentNotificationJob started - JobId: {JobId}", jobId);

            try
            {
                var expiredDocuments = await _scanService.GetExpiredDocumentsAsync();

                if (!expiredDocuments.Any())
                {
                    _logger.LogInformation("No expired documents found - JobId: {JobId}", jobId);
                    return;
                }

                _logger.LogInformation("Found {Count} expired documents to process - JobId: {JobId}",
                    expiredDocuments.Count, jobId);

                // Send commands to process expired documents via existing consumer
                var tasks = expiredDocuments.Select(doc =>
                    _bus.Send(new ProcessDocumentExpirationCommand
                    {
                        Document = doc,
                        NotificationType = NotificationType.Expired
                    }));

                await Task.WhenAll(tasks);

                _logger.LogInformation("ExpiredDocumentNotificationJob completed - JobId: {JobId}", jobId);
                context.JobDetail.JobDataMap.Put("LastSuccessfulRun", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExpiredDocumentNotificationJob - JobId: {JobId}", jobId);
                context.JobDetail.JobDataMap.Put("LastErrorTime", DateTime.UtcNow);
                context.JobDetail.JobDataMap.Put("LastError", ex.Message);
                throw new JobExecutionException(ex, refireImmediately: false);
            }
        }
    }

    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public class NearExpiredDocumentNotificationJob : IJob
    {
        private readonly ILogger<NearExpiredDocumentNotificationJob> _logger;
        private readonly IDocumentScanService _scanService;

        public NearExpiredDocumentNotificationJob(
            ILogger<NearExpiredDocumentNotificationJob> logger,
            IDocumentScanService scanService)
        {
            _logger = logger;
            _scanService = scanService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = Guid.NewGuid().ToString("N")[..8];
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
        }
    }
}
