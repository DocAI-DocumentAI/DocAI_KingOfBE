using AI.API.Payload.Response;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace AI.API.Middlewares
{
    /// <summary>
    /// Combined core middleware for correlation ID, exception handling, and basic logging
    /// </summary>
    public class CoreMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CoreMiddleware> _logger;
        private const string CorrelationIdHeaderName = "X-Correlation-Id";

        public CoreMiddleware(RequestDelegate next, ILogger<CoreMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = GetOrCreateCorrelationId(context);

            // Store correlation ID for use throughout the request
            context.Items[CorrelationIdHeaderName] = correlationId;
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;

            // Add to logging context
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method
            }))
            {
                try
                {
                    await _next(context);
                    
                    // Log successful requests
                    stopwatch.Stop();
                    if (context.Response.StatusCode >= 400)
                    {
                        _logger.LogWarning("Request completed with status {StatusCode} in {ElapsedMs}ms",
                            context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogDebug("Request completed successfully in {ElapsedMs}ms",
                            stopwatch.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                    await HandleExceptionAsync(context, ex, correlationId);
                }
            }
        }

        private string GetOrCreateCorrelationId(HttpContext context)
        {
            // Check if correlation ID is provided in request header
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) &&
                !string.IsNullOrEmpty(correlationId))
            {
                return correlationId.ToString();
            }

            // Generate new correlation ID
            return Guid.NewGuid().ToString("N")[..8]; // Short 8-character ID
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
        {
            context.Response.ContentType = "application/json";

            var response = new BaseResponse
            {
                Success = false,
                RequestId = correlationId,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ArgumentException argEx:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Invalid request parameters";
                    break;

                case UnauthorizedAccessException:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Unauthorized access";
                    break;

                case TimeoutException:
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Message = "Request timeout";
                    break;

                case TaskCanceledException:
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Message = "Request was cancelled";
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Message = "An internal server error occurred";
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
