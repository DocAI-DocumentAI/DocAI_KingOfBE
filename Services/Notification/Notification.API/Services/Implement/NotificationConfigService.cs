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
        private readonly INotificationSchedulerService? _schedulerService; // ✅ ADDED: Optional scheduler service

        // Vietnam timezone as default
        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                // Fallback for Linux/Mac
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    return TimeZoneInfo.Utc; // Last resort
                }
            }
        }
        public NotificationConfigService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            ILogger<NotificationConfigService> logger,
            IMemoryCache cache,
            INotificationSchedulerService? schedulerService = null) // ✅ ADDED: Optional injection
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _cache = cache;
            _schedulerService = schedulerService;
        }

        private static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

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
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));

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

            // ✅ STORE old values for comparison
            var oldCronExpression = config.ScanCronExpression;
            var oldQuartzEnabled = config.QuartzEnabled;

            _mapper.Map(request, config);
            config.UpdateAt = VietnamNow;

            repo.UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            _cache.Remove("notification_config");

            // ✅ ADDED: Auto-update Quartz schedule if changed
            await UpdateQuartzScheduleIfNeeded(config, oldCronExpression, oldQuartzEnabled);

            _logger.LogInformation("Notification configuration updated at {VietnamTime} (Vietnam time). " +
                "WarningDays: {Days}, Cron: '{Cron}', QuartzEnabled: {Enabled}",
                VietnamNow.ToString("yyyy-MM-dd HH:mm:ss"),
                config.WarningThresholdDays,
                config.ScanCronExpression,
                config.QuartzEnabled);

            return _mapper.Map<NotificationConfigResponse>(config);
        }

        // ✅ ADDED: Auto-update Quartz when config changes
        private async Task UpdateQuartzScheduleIfNeeded(NotificationConfig config, string oldCronExpression, bool oldQuartzEnabled)
        {
            if (_schedulerService == null)
            {
                _logger.LogDebug("Scheduler service not available - Quartz may not be configured");
                return;
            }

            try
            {
                // Check if cron expression changed
                bool cronChanged = !string.Equals(oldCronExpression, config.ScanCronExpression, StringComparison.OrdinalIgnoreCase);
                bool enabledChanged = oldQuartzEnabled != config.QuartzEnabled;

                if (cronChanged || enabledChanged)
                {
                    if (config.QuartzEnabled)
                    {
                        // Update/restart schedule with new cron
                        await _schedulerService.UpdateDocumentScanJobSchedule(config.ScanCronExpression);
                        _logger.LogInformation("✅ Updated Quartz schedule to: '{CronExpression}' (Vietnam timezone)",
                            config.ScanCronExpression);
                    }
                    else
                    {
                        // Disable/pause jobs
                        await _schedulerService.PauseAllJobs(); // ✅ Implement this method
                        _logger.LogInformation("⏸️ Paused Quartz jobs (disabled via config)");
                    }
                }
                else
                {
                    _logger.LogDebug("Quartz schedule unchanged");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to update Quartz schedule. Config saved but Quartz not updated. " +
                    "Manual restart may be required.");

                // Don't throw - config update should succeed even if Quartz update fails
            }
        }

        private async Task<NotificationConfig> CreateDefaultConfigAsync()
        {
            var defaultConfig = new NotificationConfig
            {
                ConfigKey = ApiConstants.DEFAULT_CONFIG_KEY,
                QuartzEnabled = true,
                WarningThresholdDays = 7,
                ScanCronExpression = "0 0 7 * * ?", // 7:00 AM Vietnam time
                LogRetentionDays = 90,
                CreateAt = VietnamNow
            };

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            await repo.InsertAsync(defaultConfig);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created default notification configuration at {VietnamTime} (Vietnam time)",
                VietnamNow.ToString("yyyy-MM-dd HH:mm:ss"));

            return defaultConfig;
        }

        // ✅ ADDED: Get config with Quartz status
        public async Task<object> GetConfigWithQuartzStatusAsync()
        {
            var config = await GetNotificationConfigAsync();

            object quartzStatus = "Not Available";

            if (_schedulerService != null)
            {
                try
                {
                    quartzStatus = await _schedulerService.GetSchedulerStatusAsync(); // ✅ Implement this
                }
                catch (Exception ex)
                {
                    quartzStatus = $"Error: {ex.Message}";
                }
            }

            return new
            {
                Config = config,
                QuartzStatus = quartzStatus,
                VietnamTime = VietnamNow.ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                NextScheduledRun = await GetNextScheduledRunAsync()
            };
        }

        // ✅ ADDED: Get next scheduled run time
        public async Task<DateTime?> GetNextScheduledRunAsync()
        {
            try
            {
                var config = await GetNotificationConfigAsync();

                if (!config.QuartzEnabled || string.IsNullOrEmpty(config.ScanCronExpression))
                {
                    return null;
                }

                if (!CronExpression.IsValidExpression(config.ScanCronExpression))
                {
                    return null;
                }

                var cronExpression = new CronExpression(config.ScanCronExpression);
                var vietnamNow = VietnamNow;

                // Calculate in Vietnam timezone
                var nextUtc = cronExpression.GetNextValidTimeAfter(vietnamNow.ToUniversalTime());

                if (nextUtc.HasValue)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(nextUtc.Value.DateTime, VietnamTimeZone);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating next scheduled run time");
                return null;
            }
        }
    }
}
