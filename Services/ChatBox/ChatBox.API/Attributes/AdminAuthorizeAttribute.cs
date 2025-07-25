using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace ChatBox.API.Attributes
{
    /// <summary>
    /// Authorization attribute that requires Admin role
    /// </summary>
    public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check if user has Admin role
            var userRoles = context.HttpContext.User.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList() ?? new List<string>();
            
            if (!userRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
