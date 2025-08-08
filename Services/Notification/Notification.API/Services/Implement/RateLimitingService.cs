using Microsoft.Extensions.Caching.Memory;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class RateLimitingService : IRateLimitingService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RateLimitingService> _logger;
        private readonly int _maxEmailsPerHour;
        private readonly int _maxEmailsPerDay;
        private const string HOURLY_KEY = "email_count_hourly";
        private const string DAILY_KEY = "email_count_daily";

        public RateLimitingService(
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<RateLimitingService> logger)
        {
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
            _maxEmailsPerHour = _configuration.GetValue<int>("Email:MaxPerHour", 100);
            _maxEmailsPerDay = _configuration.GetValue<int>("Email:MaxPerDay", 1000);
        }

        public async Task<bool> CanSendEmailAsync()
        {
            var hourlyCount = GetCurrentCount(HOURLY_KEY);
            var dailyCount = GetCurrentCount(DAILY_KEY);

            if (hourlyCount >= _maxEmailsPerHour)
            {
                _logger.LogWarning("Hourly email limit reached: {Count}/{Max}", hourlyCount, _maxEmailsPerHour);
                return false;
            }

            if (dailyCount >= _maxEmailsPerDay)
            {
                _logger.LogWarning("Daily email limit reached: {Count}/{Max}", dailyCount, _maxEmailsPerDay);
                return false;
            }

            return true;
        }

        public async Task RecordEmailSentAsync()
        {
            IncrementCount(HOURLY_KEY, TimeSpan.FromHours(1));
            IncrementCount(DAILY_KEY, TimeSpan.FromDays(1));

            _logger.LogDebug("Email sent recorded. Hourly: {Hourly}, Daily: {Daily}",
                GetCurrentCount(HOURLY_KEY), GetCurrentCount(DAILY_KEY));
        }

        public async Task<int> GetRemainingEmailsAsync()
        {
            var hourlyCount = GetCurrentCount(HOURLY_KEY);
            var dailyCount = GetCurrentCount(DAILY_KEY);

            var hourlyRemaining = Math.Max(0, _maxEmailsPerHour - hourlyCount);
            var dailyRemaining = Math.Max(0, _maxEmailsPerDay - dailyCount);

            return Math.Min(hourlyRemaining, dailyRemaining);
        }

        private int GetCurrentCount(string key)
        {
            return _cache.TryGetValue(key, out int count) ? count : 0;
        }

        private void IncrementCount(string key, TimeSpan expiry)
        {
            var currentCount = GetCurrentCount(key);
            _cache.Set(key, currentCount + 1, expiry);
        }
    }
}