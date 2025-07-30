using System.Text;

namespace AI.API.Middlewares
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
        private readonly HashSet<string> _sensitiveHeaders = new()
        {
            "Authorization", "X-Api-Key", "Cookie"
        };

        public RequestResponseLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip logging for health endpoints
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            // Log request
            var requestId = context.TraceIdentifier;
            await LogRequest(context, requestId);

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
                await LogResponse(context, requestId);
            }
            finally
            {
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task LogRequest(HttpContext context, string requestId)
        {
            context.Request.EnableBuffering();

            var request = context.Request;
            var requestBody = "";

            if (request.ContentLength > 0 && request.ContentLength < 100_000) // Max 100KB
            {
                request.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Seek(0, SeekOrigin.Begin);
            }

            var headers = request.Headers
                .Where(h => !_sensitiveHeaders.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            _logger.LogInformation("HTTP Request {RequestId}: {Method} {Path} {QueryString} - Headers: {@Headers} - Body: {Body}",
                requestId,
                request.Method,
                request.Path,
                request.QueryString,
                headers,
                requestBody);
        }

        private async Task LogResponse(HttpContext context, string requestId)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = "";

            if (context.Response.ContentLength < 100_000) // Max 100KB
            {
                responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            }

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            _logger.LogInformation("HTTP Response {RequestId}: {StatusCode} - Body: {Body}",
                requestId,
                context.Response.StatusCode,
                responseBody);
        }
    }
}
