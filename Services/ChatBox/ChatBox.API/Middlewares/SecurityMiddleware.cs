using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Middlewares
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var userId = GetUserId(context);
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                // Security checks for POST requests with content
                if (context.Request.Method == "POST" && context.Request.ContentLength > 0)
                {
                    context.Request.EnableBuffering();
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrEmpty(body))
                    {
                        using var scope = context.RequestServices.CreateScope();
                        var securityService = scope.ServiceProvider.GetService<ISecurityService>();
                        if (securityService != null)
                        {
                            var securityResult = await securityService.AnalyzeContentAsync(body, userId, ipAddress);

                            if (securityResult.HasSecurityIssues && securityResult.RiskScore > 0.8)
                            {
                                context.Response.StatusCode = 403;
                                await context.Response.WriteAsync("Request blocked due to security policy violation");
                                return;
                            }
                        }
                    }
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in security middleware");
                await _next(context); // Continue on error
            }
        }

        private Guid GetUserId(HttpContext context)
        {
            var userIdClaim = context.User?.FindFirst("sub")?.Value ??
                             context.User?.FindFirst("user_id")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
