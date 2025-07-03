using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChatBox.API.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CusTomAuthorize : Attribute, IAuthorizationFilter
    {

        public string[]? Roles { get; set; }
        public string[]? Departments { get; set; }
        public string[]? Permissions { get; set; } 
        public bool RequireAll { get; set; } = false;

        public CusTomAuthorize() { }

        public CusTomAuthorize(params string[] roles)
        {
            Roles = roles;
        }
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult(); 
                return;
            }

            var user = context.HttpContext.User;

            var userRole = user.FindFirst(ClaimTypes.Role)?.Value; 
            var userDepartmentName = user.FindFirst("departmentName")?.Value; 
            var userPermissions = user.FindFirst("permissions")?.Value?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            bool hasAccess = false;

            if (RequireAll)
            {
                hasAccess = CheckAllConditions(userRole, userDepartmentName, userPermissions);
            }
            else
            {
                hasAccess = CheckAnyCondition(userRole, userDepartmentName, userPermissions);
            }

            if (!hasAccess)
            {
                context.Result = new ForbidResult(); 
                return;
            }
        }
        private bool CheckAllConditions(string? userRole, string? userDepartmentName, string[] userPermissions)
        {
            bool roleCheck = Roles == null || Roles.Length == 0 || (userRole != null && Roles.Contains(userRole, StringComparer.OrdinalIgnoreCase));
            bool departmentCheck = Departments == null || Departments.Length == 0 || (userDepartmentName != null && Departments.Contains(userDepartmentName, StringComparer.OrdinalIgnoreCase));
            bool permissionCheck = Permissions == null || Permissions.Length == 0 || Permissions.All(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));

            return roleCheck && departmentCheck && permissionCheck;
        }

        private bool CheckAnyCondition(string? userRole, string? userDepartmentName, string[] userPermissions)
        {
            if (Roles != null && Roles.Length > 0 && userRole != null)
            {
                if (Roles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            if (Departments != null && Departments.Length > 0 && userDepartmentName != null)
            {
                if (Departments.Contains(userDepartmentName, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            if (Permissions != null && Permissions.Length > 0)
            {
                if (Permissions.Any(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase)))
                    return true;
            }

            if ((Roles == null || Roles.Length == 0) &&
                (Departments == null || Departments.Length == 0) &&
                (Permissions == null || Permissions.Length == 0))
            {
                return true;
            }

            return false;
        }
    }
}
