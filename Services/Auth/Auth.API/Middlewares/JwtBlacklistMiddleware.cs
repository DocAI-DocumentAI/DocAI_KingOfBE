using Auth.API.Services.Interface;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Auth.API.Payload.Response;

namespace Auth.API.Middlewares
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtBlacklistMiddleware> _logger;

        public JwtBlacklistMiddleware(RequestDelegate next, ILogger<JwtBlacklistMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Chỉ check blacklist nếu user đã authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // Resolve scoped service từ HttpContext
                var redisService = context.RequestServices.GetRequiredService<IRedisService>();

                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var jsonToken = handler.ReadJwtToken(token);
                        var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                        if (!string.IsNullOrEmpty(jti))
                        {
                            var isBlacklisted = await redisService.IsJwtBlacklistedAsync(jti);
                            if (isBlacklisted)
                            {
                                _logger.LogWarning("Blacklisted JWT token attempted access: {Jti}", jti);
                                await WriteUnauthorizedResponse(context, "Token has been revoked");
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking JWT blacklist");
                    }
                }
            }

            await _next(context);
        }

        private async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                StatusCode = 401,
                Error = message,
                Path = context.Request.Path,
                TraceId = context.TraceIdentifier,
                TimeStamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
        }
    }
}
