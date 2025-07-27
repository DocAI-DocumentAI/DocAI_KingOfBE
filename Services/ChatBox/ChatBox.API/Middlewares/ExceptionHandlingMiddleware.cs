using ChatBox.API.Services.Interfaces;
using System.Net;
using System.Security;
using System.Text.Json;

namespace ChatBox.API.Middlewares
{
    public class ExceptionHandlingMiddleware
    {

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        // ✅ ONLY inject singleton services in constructor
        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString();
            var userId = GetUserId(context);
            var ipAddress = GetClientIpAddress(context);

            // Log the exception with context
            _logger.LogError(exception,
                "Unhandled exception occurred. CorrelationId: {CorrelationId}, UserId: {UserId}, IP: {IpAddress}, Path: {Path}",
                correlationId, userId, ipAddress, context.Request.Path);

            // ✅ Create scope to get scoped services
            using var scope = context.RequestServices.CreateScope();

            // Log security event for audit (only if service is available)
            try
            {
                var auditService = scope.ServiceProvider.GetService<IAuditService>();
                if (auditService != null)
                {
                    await auditService.LogSecurityEventAsync(userId, "UnhandledException",
                        $"Unhandled exception: {exception.GetType().Name}", "medium", ipAddress,
                        new Dictionary<string, object>
                        {
                            { "CorrelationId", correlationId },
                            { "ExceptionType", exception.GetType().Name },
                            { "Path", context.Request.Path.ToString() },
                            { "Method", context.Request.Method }
                        });
                }
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Failed to log security event for exception");
            }

            // Determine response based on exception type
            var response = CreateErrorResponse(exception, correlationId);

            // Set response properties
            context.Response.StatusCode = response.StatusCode;
            context.Response.ContentType = "application/json";

            // Write response
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(json);
        }

        private ErrorResponse CreateErrorResponse(Exception exception, string correlationId)
        {
            return exception switch
            {
                ArgumentNullException or ArgumentException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Error = "Bad Request",
                    Message = "Invalid request parameters.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                UnauthorizedAccessException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized,
                    Error = "Unauthorized",
                    Message = "You are not authorized to access this resource.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                SecurityException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    Error = "Forbidden",
                    Message = "Access denied due to security policy.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                TimeoutException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.RequestTimeout,
                    Error = "Request Timeout",
                    Message = "The request took too long to process. Please try again.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                HttpRequestException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                    Error = "Service Unavailable",
                    Message = "External service is temporarily unavailable. Please try again later.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                TaskCanceledException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.RequestTimeout,
                    Error = "Request Cancelled",
                    Message = "The request was cancelled or timed out.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                },

                _ => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Error = "Internal Server Error",
                    Message = "An unexpected error occurred. Please try again later.",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        private Guid? GetUserId(HttpContext context)
        {
            try
            {
                var userIdClaim = context.User?.FindFirst("userId")?.Value;
                return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
            }
            catch
            {
                return null;
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            try
            {
                var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(xForwardedFor))
                {
                    return xForwardedFor.Split(',')[0].Trim();
                }

                var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(xRealIp))
                {
                    return xRealIp;
                }

                return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }

    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    // Extension method for easy registration
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
