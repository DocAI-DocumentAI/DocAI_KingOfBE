using System.Security.Claims;
using Auth.API.Services.Interface;

namespace Auth.API.Services.Implement
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthorizationService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ClaimsPrincipal GetCurrentUser()
        {
            return _httpContextAccessor.HttpContext?.User ?? throw new UnauthorizedAccessException("User is not authenticated");
        }

        public Guid GetCurrentUserId()
        {
            var userIdClaim = GetCurrentUser().FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid user ID in token");
            return userId;
        }

        public string? GetCurrentUserRole()
        {
            return GetCurrentUser().FindFirst(ClaimTypes.Role)?.Value;
        }

        public string? GetCurrentUserDepartment()
        {
            return GetCurrentUser().FindFirst("departmentName")?.Value;
        }

        public string[] GetCurrentUserPermissions()
        {
            var permissionsString = GetCurrentUser().FindFirst("permissions")?.Value;
            return string.IsNullOrEmpty(permissionsString) 
                ? Array.Empty<string>() 
                : permissionsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }

        public bool HasRole(string role)
        {
            var userRole = GetCurrentUserRole();
            return !string.IsNullOrEmpty(userRole) && userRole.Equals(role, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasAnyRole(params string[] roles)
        {
            var userRole = GetCurrentUserRole();
            return !string.IsNullOrEmpty(userRole) && roles.Any(r => r.Equals(userRole, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasAllRoles(params string[] roles)
        {
            var userRole = GetCurrentUserRole();
            if (string.IsNullOrEmpty(userRole)) return false;
            
            // Một user chỉ có thể có một role, nên chỉ có thể HasAllRoles nếu chỉ có 1 role được yêu cầu
            return roles.Length == 1 && roles[0].Equals(userRole, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsInDepartment(string departmentName)
        {
            var userDepartment = GetCurrentUserDepartment();
            return !string.IsNullOrEmpty(userDepartment) && userDepartment.Equals(departmentName, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsInAnyDepartment(params string[] departmentNames)
        {
            var userDepartment = GetCurrentUserDepartment();
            return !string.IsNullOrEmpty(userDepartment) && departmentNames.Any(d => d.Equals(userDepartment, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasPermission(string permission)
        {
            var userPermissions = GetCurrentUserPermissions();
            return userPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasAnyPermission(params string[] permissions)
        {
            var userPermissions = GetCurrentUserPermissions();
            return permissions.Any(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        public bool HasAllPermissions(params string[] permissions)
        {
            var userPermissions = GetCurrentUserPermissions();
            return permissions.All(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        public bool CheckAuthorization(string[]? roles = null, string[]? departments = null, string[]? permissions = null, bool requireAll = false)
        {
            if (requireAll)
            {
                // AND logic - phải thỏa mãn tất cả điều kiện
                bool roleCheck = roles == null || roles.Length == 0 || HasAnyRole(roles);
                bool departmentCheck = departments == null || departments.Length == 0 || IsInAnyDepartment(departments);
                bool permissionCheck = permissions == null || permissions.Length == 0 || HasAllPermissions(permissions);

                return roleCheck && departmentCheck && permissionCheck;
            }
            else
            {
                // OR logic - chỉ cần thỏa mãn một điều kiện
                bool hasAccess = false;

                if (roles != null && roles.Length > 0)
                    hasAccess |= HasAnyRole(roles);

                if (departments != null && departments.Length > 0)
                    hasAccess |= IsInAnyDepartment(departments);

                if (permissions != null && permissions.Length > 0)
                    hasAccess |= HasAnyPermission(permissions);

                // Nếu không có điều kiện nào được set, cho phép truy cập (chỉ cần authenticated)
                if ((roles == null || roles.Length == 0) && 
                    (departments == null || departments.Length == 0) && 
                    (permissions == null || permissions.Length == 0))
                {
                    return true;
                }

                return hasAccess;
            }
        }
    }
}
