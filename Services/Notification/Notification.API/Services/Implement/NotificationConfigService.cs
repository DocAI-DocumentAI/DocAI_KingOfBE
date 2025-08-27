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

            // Calculate next run times
            response.NextExpiredNotificationTime = await GetNextRunTimeAsync(config.ExpiredNotificationCron);
            response.NextNearExpiredNotificationTime = await GetNextRunTimeAsync(config.NearExpiredNotificationCron);

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));

            return response;
        }

        public async Task<NotificationConfigResponse> UpdateNotificationConfigAsync(NotificationConfigRequest request)
        {
            var vietnamNow = TimeZoneHelper.VietnamNow; // ✅ For logging
            _logger.LogInformation("🔧 Updating notification config at Vietnam time: {VietnamTime}",
                vietnamNow.ToString("yyyy-MM-dd HH:mm:ss"));

            // Validate cron expressions
            if (!CronExpression.IsValidExpression(request.ExpiredNotificationCron))
                throw new BadHttpRequestException($"Invalid expired notification cron expression: {request.ExpiredNotificationCron}");

            if (!CronExpression.IsValidExpression(request.NearExpiredNotificationCron))
                throw new BadHttpRequestException($"Invalid near-expired notification cron expression: {request.NearExpiredNotificationCron}");

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await repo.SingleOrDefaultAsync(predicate: c => c.ConfigKey == "Default");

            if (config == null)
            {
                _logger.LogInformation("🆕 Config not found, creating default config");
                config = await CreateDefaultConfigAsync();
            }

            var oldExpiredCron = config.ExpiredNotificationCron;
            var oldNearExpiredCron = config.NearExpiredNotificationCron;
            var oldQuartzEnabled = config.QuartzEnabled;

            _mapper.Map(request, config);
            config.UpdateAt = TimeZoneHelper.UtcNow; 
            repo.UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            _cache.Remove("notification_config");

            // Update Quartz schedules if needed
            await UpdateQuartzSchedulesIfNeeded(config, oldExpiredCron, oldNearExpiredCron, oldQuartzEnabled);

            var response = _mapper.Map<NotificationConfigResponse>(config);
            response.NextExpiredNotificationTime = await GetNextRunTimeAsync(config.ExpiredNotificationCron);
            response.NextNearExpiredNotificationTime = await GetNextRunTimeAsync(config.NearExpiredNotificationCron);

            return response;
        }

        private async Task UpdateQuartzSchedulesIfNeeded(NotificationConfig config, string oldExpiredCron,
            string oldNearExpiredCron, bool oldQuartzEnabled)
        {
            if (_schedulerService == null)
            {
                _logger.LogWarning("Scheduler service not available");
                return;
            }

            try
            {
                bool expiredCronChanged = !string.Equals(oldExpiredCron, config.ExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool nearExpiredCronChanged = !string.Equals(oldNearExpiredCron, config.NearExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool enabledChanged = oldQuartzEnabled != config.QuartzEnabled;

                // Update expired notification schedule if changed
                if (expiredCronChanged)
                {
                    await _schedulerService.UpdateExpiredDocumentJobSchedule(config.ExpiredNotificationCron);
                    _logger.LogInformation("Updated expired document schedule to: '{CronExpression}'",
                        config.ExpiredNotificationCron);
                }

                // Update near-expired notification schedule if changed
                if (nearExpiredCronChanged)
                {
                    await _schedulerService.UpdateNearExpiredDocumentJobSchedule(config.NearExpiredNotificationCron);
                    _logger.LogInformation("Updated near-expired document schedule to: '{CronExpression}'",
                        config.NearExpiredNotificationCron);
                }

                // Handle enabled/disabled state
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
                throw; // Re-throw to let caller know update failed
            }
        }

        private async Task<NotificationConfig> CreateDefaultConfigAsync()
        {
            var utcNow = TimeZoneHelper.UtcNow; // ✅ Use unified helper for database

            var defaultConfig = new NotificationConfig
            {
                ConfigKey = "Default",
                QuartzEnabled = true,
                WarningThresholdDays = 7,
                ExpiredNotificationCron = "0 0 6 * * ?",        // 6:00 AM daily
                NearExpiredNotificationCron = "0 0 6 * * ?",     // 6:00 AM daily  
                EnableExpiredNotifications = true,
                EnableNearExpiredNotifications = true,
                NearExpiredMode = NotificationMode.Daily,
                LogRetentionDays = 90,
                CreateAt = utcNow // ✅ Store UTC in database
            };

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            await repo.InsertAsync(defaultConfig);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("✅ Created default notification config with 6:00 AM schedule");

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
