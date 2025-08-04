using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Document.API.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CustomAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        public string[]? Roles { get; set; }
        public string[]? Departments { get; set; }
        public string[]? Permissions { get; set; }
        public bool RequireAll { get; set; } = false;

        public CustomAuthorizeAttribute()
        {
            // Kế thừa từ AuthorizeAttribute để trigger JWT authentication
        }

        public CustomAuthorizeAttribute(params string[] roles)
        {
            Roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Kiểm tra nếu user đã được authorize bởi AuthorizeAttribute
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                // AuthorizeAttribute sẽ handle việc này
                return;
            }

            var user = context.HttpContext.User;

            // Debug: In tất cả claims
            var allClaims = user.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            Console.WriteLine($"All claims: {string.Join(", ", allClaims)}");

            var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
            Console.WriteLine($"Found role: {userRole ?? "null"}");

            // Thử cách khác nếu ClaimTypes.Role không work
            if (string.IsNullOrEmpty(userRole))
            {
                userRole = user.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
                Console.WriteLine($"Found role with full claim type: {userRole ?? "null"}");
            }

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





