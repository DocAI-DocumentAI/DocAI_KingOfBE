using ChatBox.API.Services.Implement;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Extensions
{
    public static class RateLimitingExtensions
    {
        public static async Task<bool> CheckAndRecordAsync(this IRateLimitingService rateLimitingService,
            Guid userId, string action)
        {
            var isWithinLimit = await rateLimitingService.IsWithinLimitAsync(userId, action);

            if (isWithinLimit)
            {
                await rateLimitingService.RecordRequestAsync(userId, action);
            }

            return isWithinLimit;
        }

        public static async Task<RateLimitResult> CheckLimitWithDetailsAsync(this IRateLimitingService rateLimitingService,
            Guid userId, string action)
        {
            // This would be implemented if IRateLimitingService had a method that returns detailed info
            var isWithinLimit = await rateLimitingService.IsWithinLimitAsync(userId, action);

            return new RateLimitResult
            {
                IsAllowed = isWithinLimit,
                Reason = isWithinLimit ? null : "Rate limit exceeded"
            };
        }
    }
}
