namespace ChatBox.API.Middlewares
{
    public class CorrelationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationMiddleware> _logger;

        public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                               ?? Guid.NewGuid().ToString();

            context.Response.Headers.Add("X-Correlation-ID", correlationId);

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                await _next(context);
            }
        }
    }
}
