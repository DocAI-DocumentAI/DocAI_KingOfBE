using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Notification.API.Services.Implement
{
    public class NotificationConfigService : INotificationConfigService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<NotificationConfigService> _logger;
        private readonly IMemoryCache _cache;

        public NotificationConfigService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            ILogger<NotificationConfigService> logger,
            IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _cache = cache;
        }

        public async Task<NotificationConfigResponse> GetNotificationConfigAsync()
        {
            const string cacheKey = "notification_config";
            if (_cache.TryGetValue(cacheKey, out NotificationConfigResponse? cached) && cached != null)
                return cached;

            var config = await _unitOfWork.GetRepository<NotificationConfig>()
                .SingleOrDefaultAsync(predicate: c => c.ConfigKey == ApiConstants.DEFAULT_CONFIG_KEY);

            if (config == null)
            {
                config = await CreateDefaultConfigAsync();
            }

            var response = _mapper.Map<NotificationConfigResponse>(config);
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(ApiConstants.CACHE_DURATION_MINUTES));

            return response;
        }

        public async Task<NotificationConfigResponse> UpdateNotificationConfigAsync(NotificationConfigRequest request)
        {
            if (!CronExpression.IsValidExpression(request.ScanCronExpression))
                throw new BadHttpRequestException($"Invalid cron expression: {request.ScanCronExpression}");

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await repo.SingleOrDefaultAsync(predicate: c => c.ConfigKey == ApiConstants.DEFAULT_CONFIG_KEY);

            if (config == null)
            {
                config = await CreateDefaultConfigAsync();
            }

            _mapper.Map(request, config);
            config.UpdateAt = DateTime.UtcNow;

            repo.UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            _cache.Remove("notification_config");

            _logger.LogInformation("Notification configuration updated");
            return _mapper.Map<NotificationConfigResponse>(config);
        }

        private async Task<NotificationConfig> CreateDefaultConfigAsync()
        {
            var defaultConfig = new NotificationConfig
            {
                ConfigKey = ApiConstants.DEFAULT_CONFIG_KEY,
                QuartzEnabled = true,
                WarningThresholdDays = 7,
                ScanCronExpression = "0 0 7 * * ?",
                LogRetentionDays = 90
            };

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            await repo.InsertAsync(defaultConfig);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created default notification configuration");
            return defaultConfig;
        }
    }
}