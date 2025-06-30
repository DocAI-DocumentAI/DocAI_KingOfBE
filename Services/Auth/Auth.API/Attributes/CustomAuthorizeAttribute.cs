using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Auth.API.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CustomAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public string[]? Roles { get; set; }
        public string[]? Departments { get; set; }
        public string[]? Permissions { get; set; }
        public bool RequireAll { get; set; } = false; // false = OR logic, true = AND logic

        public CustomAuthorizeAttribute()
        {
        }

        public CustomAuthorizeAttribute(params string[] roles)
        {
            Roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Kiểm tra xem user đã được authenticate chưa
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var user = context.HttpContext.User;

            // Lấy thông tin từ claims
            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
            var userDepartmentId = user.FindFirst("departmentId")?.Value;
            var userDepartmentName = user.FindFirst("departmentName")?.Value;
            var userPermissions = user.FindFirst("permissions")?.Value?.Split(',') ?? Array.Empty<string>();

            bool hasAccess = false;

            if (RequireAll)
            {
                // AND logic - phải thỏa mãn tất cả điều kiện
                hasAccess = CheckAllConditions(userRole, userDepartmentName, userPermissions);
            }
            else
            {
                // OR logic - chỉ cần thỏa mãn một điều kiện
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
            // Kiểm tra role
            if (Roles != null && Roles.Length > 0 && userRole != null)
            {
                if (Roles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            // Kiểm tra department
            if (Departments != null && Departments.Length > 0 && userDepartmentName != null)
            {
                if (Departments.Contains(userDepartmentName, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            // Kiểm tra permissions
            if (Permissions != null && Permissions.Length > 0)
            {
                if (Permissions.Any(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase)))
                    return true;
            }

            // Nếu không có điều kiện nào được set, cho phép truy cập (chỉ cần authenticated)
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
