namespace ChatBox.API.Middlewares
{
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMiddleware> _logger;

        public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds;

                if (elapsed > 1000) // Log slow requests
                {
                    _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                        context.Request.Method, context.Request.Path, elapsed);
                }

                context.Response.Headers.Add("X-Response-Time", elapsed.ToString());
            }
        }
    }
}

