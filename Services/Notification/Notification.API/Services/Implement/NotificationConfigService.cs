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

namespace Notification.API.Services.Implement
{
    public class NotificationConfigService : INotificationConfigService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<NotificationConfigService> _logger;
        private readonly IMemoryCache _cache;
        private readonly INotificationSchedulerService? _schedulerService;

        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    return TimeZoneInfo.Utc;
                }
            }
        }

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
            // Validate cron expressions
            if (!CronExpression.IsValidExpression(request.ExpiredNotificationCron))
                throw new BadHttpRequestException($"Invalid expired notification cron expression: {request.ExpiredNotificationCron}");

            if (!CronExpression.IsValidExpression(request.NearExpiredNotificationCron))
                throw new BadHttpRequestException($"Invalid near-expired notification cron expression: {request.NearExpiredNotificationCron}");

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            var config = await repo.SingleOrDefaultAsync(predicate: c => c.ConfigKey == "Default");

            if (config == null)
            {
                config = await CreateDefaultConfigAsync();
            }

            var oldExpiredCron = config.ExpiredNotificationCron;
            var oldNearExpiredCron = config.NearExpiredNotificationCron;
            var oldQuartzEnabled = config.QuartzEnabled;

            _mapper.Map(request, config);
            config.UpdateAt = VietnamTimeHelper.GetUtcNow();
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
                _logger.LogDebug("Scheduler service not available");
                return;
            }

            try
            {
                bool expiredCronChanged = !string.Equals(oldExpiredCron, config.ExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool nearExpiredCronChanged = !string.Equals(oldNearExpiredCron, config.NearExpiredNotificationCron, StringComparison.OrdinalIgnoreCase);
                bool enabledChanged = oldQuartzEnabled != config.QuartzEnabled;

                if (enabledChanged)
                {
                    if (config.QuartzEnabled)
                    {
                        await _schedulerService.ResumeAllJobs();
                        _logger.LogInformation("✅ Resumed all Quartz jobs");
                    }
                    else
                    {
                        await _schedulerService.PauseAllJobs();
                        _logger.LogInformation("⏸️ Paused all Quartz jobs");
                    }
                }

                if (config.QuartzEnabled)
                {
                    if (expiredCronChanged)
                    {
                        await _schedulerService.UpdateExpiredDocumentJobSchedule(config.ExpiredNotificationCron);
                        _logger.LogInformation("✅ Updated expired document schedule to: '{CronExpression}'",
                            config.ExpiredNotificationCron);
                    }

                    if (nearExpiredCronChanged)
                    {
                        await _schedulerService.UpdateNearExpiredDocumentJobSchedule(config.NearExpiredNotificationCron);
                        _logger.LogInformation("✅ Updated near-expired document schedule to: '{CronExpression}'",
                            config.NearExpiredNotificationCron);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to update Quartz schedules");
            }
        }

        private async Task<NotificationConfig> CreateDefaultConfigAsync()
        {
            var defaultConfig = new NotificationConfig
            {
                ConfigKey = "Default",
                QuartzEnabled = true,
                WarningThresholdDays = 7,
                ExpiredNotificationCron = "0 0 8 * * ?",
                NearExpiredNotificationCron = "0 0 9 * * MON",
                EnableExpiredNotifications = true,
                EnableNearExpiredNotifications = true,
                NearExpiredMode = NotificationMode.Weekly,
                LogRetentionDays = 90,
                CreateAt = VietnamTimeHelper.GetUtcNow()
            };

            var repo = _unitOfWork.GetRepository<NotificationConfig>();
            await repo.InsertAsync(defaultConfig);
            await _unitOfWork.CommitAsync();

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

                var cron = new CronExpression(cronExpression);
                var vietnamNow = VietnamTimeHelper.GetUtcNow();
                var nextUtc = cron.GetNextValidTimeAfter(vietnamNow.ToUniversalTime());

                if (nextUtc.HasValue)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(nextUtc.Value.DateTime, VietnamTimeZone);
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

            return new
            {
                Config = config,
                QuartzStatus = quartzStatus,
                VietnamTime = VietnamTimeHelper.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss (dddd)"),
                TimeZone = "SE Asia Standard Time (GMT+7)"
            };
        }
    }
}
