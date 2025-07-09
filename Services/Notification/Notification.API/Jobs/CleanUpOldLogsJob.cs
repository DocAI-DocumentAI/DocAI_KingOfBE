using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;
using Polly;
using Quartz;

namespace Notification.API.Jobs
{
    [DisallowConcurrentExecution]
    public class CleanUpOldLogsJob : IJob
    {
        private readonly INotificationLogService _logService;
        private readonly ILogger<CleanUpOldLogsJob> _logger;


        public CleanUpOldLogsJob(
            INotificationLogService logService,
            ILogger<CleanUpOldLogsJob> logger)
        {
            _logService = logService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _logService.CleanUpOldLogsAsync();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
