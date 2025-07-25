using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace ChatBox.API.Middlewares
{
    /// <summary>
    /// Middleware for rate limiting API requests
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            IRateLimitingService rateLimitingService,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _rateLimitingService = rateLimitingService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Extract user ID from context (auth, claims, etc.)
                var userId = GetUserIdFromContext(context);
                if (userId == Guid.Empty)
                {
                    await _next(context);
                    return;
                }

                // Determine action from request path/method
                var action = DetermineActionFromRequest(context);
                if (string.IsNullOrEmpty(action))
                {
                    await _next(context);
                    return;
                }

                // Check rate limit
                var isWithinLimit = await _rateLimitingService.IsWithinLimitAsync(userId, action);

                if (!isWithinLimit)
                {
                    _logger.LogWarning("Rate limit exceeded for user {UserId}, action {Action}", userId, action);

                    context.Response.StatusCode = 429; // Too Many Requests
                    context.Response.Headers.Add("Retry-After", "60");
                    await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
                    return;
                }

                // Record the request
                await _rateLimitingService.RecordRequestAsync(userId, action);

                // Continue to next middleware
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting middleware");
                // On error, allow the request to continue
                await _next(context);
            }
        }

        private Guid GetUserIdFromContext(HttpContext context)
        {
            try
            {
                // Extract from JWT claims, session, etc.
                var userIdClaim = context.User?.FindFirst("sub")?.Value ??
                                 context.User?.FindFirst("user_id")?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    return userId;
                }

                return Guid.Empty;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        private string DetermineActionFromRequest(HttpContext context)
        {
            try
            {
                var path = context.Request.Path.Value?.ToLower();
                var method = context.Request.Method.ToUpper();

                return (method, path) switch
                {
                    ("POST", var p) when p.Contains("/chat/send") => "send_message",
                    ("POST", var p) when p.Contains("/chat/stream") => "start_streaming",
                    ("GET", var p) when p.Contains("/chat/history") => "get_history",
                    ("PUT", var p) when p.Contains("/preferences") => "update_preferences",
                    ("GET", var p) when p.Contains("/search") => "search",
                    _ => "api_call"
                };
            }
            catch
            {
                return "api_call";
            }
        }
    }
}