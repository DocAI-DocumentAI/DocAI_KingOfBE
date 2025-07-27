using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Middlewares
{
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditMiddleware> _logger;

        public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;
            var userId = GetUserId(context);
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var path = context.Request.Path;
            var method = context.Request.Method;

            try
            {
                await _next(context);

                // Log successful requests
                if (ShouldAudit(path, method))
                {
                    using var scope = context.RequestServices.CreateScope();
                    var auditService = scope.ServiceProvider.GetService<IAuditService>();
                    if (auditService != null)
                    {
                        await auditService.LogAsync(userId, $"{method}_{path}", "API_Request", context.TraceIdentifier,
                            null, new
                            {
                                Path = path,
                                Method = method,
                                StatusCode = context.Response.StatusCode,
                                Duration = DateTime.UtcNow - startTime
                            }, ipAddress, userAgent);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log failed requests
                try
                {
                    using var scope = context.RequestServices.CreateScope();
                    var auditService = scope.ServiceProvider.GetService<IAuditService>();
                    if (auditService != null)
                    {
                        await auditService.LogAsync(userId, $"{method}_{path}_ERROR", "API_Request", context.TraceIdentifier,
                            null, new
                            {
                                Path = path,
                                Method = method,
                                Error = ex.Message,
                                Duration = DateTime.UtcNow - startTime
                            }, ipAddress, userAgent);
                    }
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "Failed to log audit event for failed request");
                }

                throw;
            }
        }

        private bool ShouldAudit(string path, string method)
        {
            // Audit important endpoints
            var auditPaths = new[] { "/api/chat/send", "/api/chat/sessions", "/api/preferences" };
            return auditPaths.Any(p => path.Contains(p)) || method == "DELETE";
        }

        private Guid GetUserId(HttpContext context)
        {
            var userIdClaim = context.User?.FindFirst("sub")?.Value ??
                             context.User?.FindFirst("user_id")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
