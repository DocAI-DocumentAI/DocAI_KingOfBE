using Auth.API.Services.Interface;
using System.Net;

namespace Auth.API.Middlewares
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private const int RATE_LIMIT = 10;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Resolve scoped service từ HttpContext
            var redisService = context.RequestServices.GetRequiredService<IRedisService>();

            var endpoint = context.Request.Path.Value;
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Rate limit by IP
            var ipKey = $"ratelimit:ip:{clientIp}:{endpoint}";
            if (!await redisService.CheckRateLimitAsync(ipKey, RATE_LIMIT, TimeSpan.FromMinutes(1)))
            {
                await WriteRateLimitResponse(context, "IP rate limit exceeded");
                return;
            }

            // Rate limit by User ID if authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst("userId")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var userKey = $"ratelimit:user:{userId}:{endpoint}";
                    if (!await redisService.CheckRateLimitAsync(userKey, RATE_LIMIT, TimeSpan.FromMinutes(1)))
                    {
                        await WriteRateLimitResponse(context, "User rate limit exceeded");
                        return;
                    }
                }
            }

            await _next(context);
        }

        private async Task WriteRateLimitResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            var response = new
            {
                StatusCode = 429,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    }
}
