using System.Collections.Concurrent;
using System.Net;

namespace AI.API.Middlewares
{
    public class SimpleRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SimpleRateLimitMiddleware> _logger;
        private readonly ConcurrentDictionary<string, ClientRequestInfo> _clients;
        private readonly Timer _cleanupTimer;

        public SimpleRateLimitMiddleware(RequestDelegate next, ILogger<SimpleRateLimitMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _clients = new ConcurrentDictionary<string, ClientRequestInfo>();
            
            // Cleanup expired entries every minute
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientId = GetClientIdentifier(context);
            var endpoint = GetEndpointIdentifier(context);
            var key = $"{clientId}:{endpoint}";

            // Get rate limit configuration for this endpoint
            var rateLimitConfig = GetRateLimitConfig(context);
            
            if (rateLimitConfig != null)
            {
                var clientInfo = _clients.GetOrAdd(key, _ => new ClientRequestInfo());

                bool isLimitExceeded = false;
                int remaining = 0;
                DateTime resetTime = DateTime.UtcNow;

                lock (clientInfo)
                {
                    var now = DateTime.UtcNow;
                    var windowStart = now.AddMinutes(-rateLimitConfig.WindowInMinutes);

                    // Remove old requests outside the window
                    clientInfo.RequestTimes.RemoveAll(time => time < windowStart);

                    // Check if limit exceeded
                    if (clientInfo.RequestTimes.Count >= rateLimitConfig.MaxRequests)
                    {
                        isLimitExceeded = true;
                    }
                    else
                    {
                        // Add current request
                        clientInfo.RequestTimes.Add(now);
                    }

                    remaining = Math.Max(0, rateLimitConfig.MaxRequests - clientInfo.RequestTimes.Count);
                    resetTime = clientInfo.RequestTimes.FirstOrDefault().AddMinutes(rateLimitConfig.WindowInMinutes);
                }

                if (isLimitExceeded)
                {
                    _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}",
                                     clientId, endpoint);

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] = (rateLimitConfig.WindowInMinutes * 60).ToString();

                    await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
                    return;
                }

                // Add rate limit headers
                context.Response.Headers["X-RateLimit-Limit"] = rateLimitConfig.MaxRequests.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
                context.Response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)resetTime).ToUnixTimeSeconds().ToString();
            }

            await _next(context);
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try to get user ID first
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Fall back to IP address
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            // Handle forwarded headers
            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    ipAddress = forwardedFor.Split(',')[0].Trim();
                }
            }

            return $"ip:{ipAddress}";
        }

        private string GetEndpointIdentifier(HttpContext context)
        {
            return $"{context.Request.Method}:{context.Request.Path}";
        }

        private RateLimitConfig? GetRateLimitConfig(HttpContext context)
        {
            // Check if endpoint has RateLimitAttribute
            var endpoint = context.GetEndpoint();
            var rateLimitAttribute = endpoint?.Metadata.GetMetadata<AI.API.Atributte.RateLimitAttribute>();
            
            if (rateLimitAttribute != null)
            {
                return new RateLimitConfig
                {
                    MaxRequests = rateLimitAttribute.MaxRequests,
                    WindowInMinutes = rateLimitAttribute.WindowInMinutes
                };
            }

            // Default rate limit for all endpoints (more conservative)
            return new RateLimitConfig
            {
                MaxRequests = 30,
                WindowInMinutes = 1
            };
        }

        private void CleanupExpiredEntries(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();

            foreach (var kvp in _clients)
            {
                var clientInfo = kvp.Value;
                lock (clientInfo)
                {
                    // Remove requests older than 1 hour
                    clientInfo.RequestTimes.RemoveAll(time => time < now.AddHours(-1));
                    
                    // If no recent requests, mark for removal
                    if (!clientInfo.RequestTimes.Any())
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
            }

            // Remove expired entries
            foreach (var key in expiredKeys)
            {
                _clients.TryRemove(key, out _);
            }

            if (expiredKeys.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    public class ClientRequestInfo
    {
        public List<DateTime> RequestTimes { get; set; } = new();
    }

    public class RateLimitConfig
    {
        public int MaxRequests { get; set; }
        public int WindowInMinutes { get; set; }
    }
}
