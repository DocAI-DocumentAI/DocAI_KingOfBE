using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AI.API.Atributte
{
    public class RateLimitAttribute : ActionFilterAttribute
    {
        private readonly string _name;
        private readonly int _limit;
        private readonly int _windowSeconds;

        public RateLimitAttribute(string name = "default", int limit = 10, int windowSeconds = 60)
        {
            _name = name;
            _limit = limit;
            _windowSeconds = windowSeconds;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"ratelimit_{_name}_{ipAddress}";

            var requestCount = await cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_windowSeconds);
                return 0;
            });

            if (requestCount >= _limit)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = "Rate limit exceeded",
                    retryAfter = _windowSeconds
                })
                {
                    StatusCode = 429
                };
                return;
            }

            cache.Set(key, requestCount + 1, TimeSpan.FromSeconds(_windowSeconds));
            await next();
        }
    }
}
