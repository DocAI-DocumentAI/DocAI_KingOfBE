using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AI.API.Atributte
{
    public class RateLimitAttribute : ActionFilterAttribute
    {
        public int MaxRequests { get; set; } = 10;
        public int WindowInMinutes { get; set; } = 1;

        public RateLimitAttribute(int maxRequests = 10, int windowInMinutes = 1)
        {
            MaxRequests = maxRequests;
            WindowInMinutes = windowInMinutes;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Rate limiting is handled by SimpleRateLimitMiddleware
            // This attribute just provides metadata for the middleware
            await next();
        }
    }
}
