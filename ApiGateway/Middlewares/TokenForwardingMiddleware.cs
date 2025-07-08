using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ApiGateway.Middlewares
{
    public class TokenForwardingMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenForwardingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Lấy token từ header Authorization
            var authHeader = context.Request.Headers["Authorization"].ToString();

            if (!string.IsNullOrEmpty(authHeader))
            {
                // Đảm bảo token có tiền tố Bearer
                if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    authHeader = "Bearer " + authHeader;
                    context.Request.Headers["Authorization"] = authHeader;
                }

                // Log token để debug
                Console.WriteLine($"Forwarding token: {authHeader}");

                // Đảm bảo token được chuyển tiếp đến các service khác
                context.Items["OriginalAuthorizationHeader"] = authHeader;
            }
            else
            {
                Console.WriteLine("No Authorization header found");
            }

            await _next(context);
        }
    }
}

