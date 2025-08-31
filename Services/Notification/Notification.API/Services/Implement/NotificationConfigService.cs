using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Shared.Utils;

namespace Notification.API.Services.Implement
{
    public class NotificationConfigService : INotificationConfigService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<NotificationConfigService> _logger;
        private readonly IMemoryCache _cache;
        private readonly INotificationSchedulerService? _schedulerService;

        public NotificationConfigService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            ILogger<NotificationConfigService> logger,
            IMemoryCache cache,
            INotificationSchedulerService? schedulerService = null)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _cache = cache;
            _schedulerService = schedulerService;
        }


        public async Task<NotificationConfigResponse> GetNotificationConfigAsync()
        {
            const string cacheKey = "notification_config";

            if (_cache.TryGetValue(cacheKey, out NotificationConfigResponse? cached) && cached != null)
                return cached;

            var config = await _unitOfWork.GetRepository<NotificationConfig>()
                .SingleOrDefaultAsync(predicate: c => c.ConfigKey == "Default");

            if (config == null)
            {
                config = await CreateDefaultConfigAsync();
            }

            var response = _mapper.Map<NotificationConfigResponse>(config);

            // Calculate next run times - REMOVED ExpiredNotificationTime
            response.NextNearExpiredNotificationTime = await GetNextRunTimeAsync(config.NearExpiredNotificationCron);
            response.NextDocumentStatusUpdateTime = await GetNextRunTimeAsync(config.DocumentStatusUpdateCron);

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));

            return response;
        }

        public async Task<NotificationConfigResponse> UpdateNotificationConfigAsync(NotificationConfigRequest request)
        {
            var vietnamNow = TimeZoneHelper.VietnamNow;
            _logger.LogInformation("🔧 Updating notification config at Vietnam time: {VietnamTime}",
                vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"));

            // Validate cron expressions - REMOVED ExpiredNotificationCron validation
            if (!CronExpression.IsValidExpression(request.NearExpiredNotificationCron))
                throw new BadHttpRequestException($"Invalid near-expired notification cron expression: {request.NearExpiredNotificationCron}");

            if (!CronExpression.IsValidExpression(request.DocumentStatusUpdateCron))
                throw new BadHttpRequestException($"Invalid document status update cron expression: {request.DocumentStatusUpdateCron}");

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await repo.SingleOrDefaultAsync(predicate: c => c.ConfigKey == "Default");

            if (config == null)
            {
                _logger.LogInformation("🆕 Config not found, creating default config");
                config = await CreateDefaultConfigAsync();
            }

            var oldNearExpiredCron = config.NearExpiredNotificationCron;
            var oldStatusUpdateCron = config.DocumentStatusUpdateCron;
            var oldQuartzEnabled = config.QuartzEnabled;

            _mapper.Map(request, config);
            config.UpdateAt = TimeZoneHelper.UtcNow;
            repo.UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            _cache.Remove("notification_config");

            // Update Quartz schedules if needed
            await UpdateQuartzSchedulesIfNeeded(config, oldNearExpiredCron, oldStatusUpdateCron, oldQuartzEnabled);

            var response = _mapper.Map<NotificationConfigResponse>(config);
            response.NextNearExpiredNotificationTime = await GetNextRunTimeAsync(config.NearExpiredNotificationCron);
            response.NextDocumentStatusUpdateTime = await GetNextRunTimeAsync(config.DocumentStatusUpdateCron);

            return response;
        }

        private async Task UpdateQuartzSchedulesIfNeeded(NotificationConfig config,
            string oldNearExpiredCron, string oldStatusUpdateCron, bool oldQuartzEnabled)
        {
            if (_schedulerService == null)
            {
                _logger.LogWarning("Scheduler service not available");
                return;
            }

            try
            {
                bool nearExpiredCronChanged = !string.Equals(oldNearExpiredCron, config.NearExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool statusUpdateCronChanged = !string.Equals(oldStatusUpdateCron, config.DocumentStatusUpdateCron, StringComparison.OrdinalIgnoreCase);
                bool enabledChanged = oldQuartzEnabled != config.QuartzEnabled;

                // REMOVED: ExpiredNotificationCron update

                if (nearExpiredCronChanged)
                {
                    await _schedulerService.UpdateNearExpiredDocumentJobSchedule(config.NearExpiredNotificationCron);
                    _logger.LogInformation("Updated near-expired document schedule to: '{CronExpression}'",
                        config.NearExpiredNotificationCron);
                }

                if (statusUpdateCronChanged)
                {
                    await _schedulerService.UpdateDocumentStatusUpdateJobSchedule(config.DocumentStatusUpdateCron);
                    _logger.LogInformation("Updated document status update schedule to: '{CronExpression}'",
                        config.DocumentStatusUpdateCron);
                }

                if (enabledChanged)
                {
                    if (config.QuartzEnabled)
                    {
                        await _schedulerService.ResumeAllJobs();
                        _logger.LogInformation("Resumed all notification jobs");
                    }
                    else
                    {
                        await _schedulerService.PauseAllJobs();
                        _logger.LogInformation("Paused all notification jobs");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Quartz schedules");
                throw;
            }
        }

        private async Task<NotificationConfig> CreateDefaultConfigAsync()
        {
            var utcNow = TimeZoneHelper.UtcNow;

            var defaultConfig = new NotificationConfig
            {
                ConfigKey = "Default",
                QuartzEnabled = true,
                WarningThresholdDays = 7,
                NearExpiredNotificationCron = "0 0 6 * * ?",         // 6:00 AM daily
                DocumentStatusUpdateCron = "0 0 0 * * ?",            // Midnight daily
                EnableExpiredNotifications = true,
                EnableNearExpiredNotifications = true,
                LogRetentionDays = 90,
                CreateAt = utcNow
            };

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            await repo.InsertAsync(defaultConfig);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("✅ Created default notification config");

            return defaultConfig;
        }

        public async Task<DateTime?> GetNextRunTimeAsync(string cronExpression)
        {
            try
            {
                if (string.IsNullOrEmpty(cronExpression) || !CronExpression.IsValidExpression(cronExpression))
                {
                    return null;
                }

                _logger.LogDebug("Calculating next run time for cron: {Cron}", cronExpression);

                var cron = new CronExpression(cronExpression);

                // Use Vietnam time directly without setting timezone on cron
                var vietnamNow = TimeZoneHelper.VietnamNow;
                var nextVietnam = cron.GetNextValidTimeAfter(vietnamNow);

                if (nextVietnam.HasValue)
                {
                    var result = nextVietnam.Value.DateTime;

                    _logger.LogDebug("Cron calculation: VietnamNow={VietnamNow}, NextRun={NextRun}",
                        vietnamNow, result);

                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating next run time for cron: {Cron}", cronExpression);
                return null;
            }
        }

        public async Task<object> GetConfigWithStatusAsync()
        {
            var config = await GetNotificationConfigAsync();

            object quartzStatus = "Not Available";
            if (_schedulerService != null)
            {
                try
                {
                    quartzStatus = await _schedulerService.GetSchedulerStatusAsync();
                }
                catch (Exception ex)
                {
                    quartzStatus = $"Error: {ex.Message}";
                }
            }

            var vietnamNow = TimeZoneHelper.VietnamNow; // ✅ Use unified helper

            return new
            {
                Config = config,
                QuartzStatus = quartzStatus,
                VietnamTime = vietnamNow.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                TimeZone = TimeZoneHelper.VietnamTimeZone.Id,
                TimezoneInfo = TimeZoneHelper.GetTimezoneInfo()
            };
        }
    }
}
