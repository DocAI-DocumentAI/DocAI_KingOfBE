using System.Security.Claims;

namespace Notification.API.Utils
{
    /// <summary>
    /// Helper utility for extracting user information from JWT tokens
    /// </summary>
    public static class JwtHelper
    {
        /// <summary>
        /// Extracts user ID from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>User ID as string, or null if not found</returns>
        public static string? GetUserId(ClaimsPrincipal user)
        {
            return user?.FindFirst("userId")?.Value;
        }

        /// <summary>
        /// Extracts user email from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>User email as string, or null if not found</returns>
        public static string? GetUserEmail(ClaimsPrincipal user)
        {
            return user?.FindFirst("email")?.Value;
        }

        /// <summary>
        /// Extracts user full name from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>User full name as string, or null if not found</returns>
        public static string? GetUserFullName(ClaimsPrincipal user)
        {
            return user?.FindFirst("fullName")?.Value;
        }

        /// <summary>
        /// Extracts department ID from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>Department ID as string, or null if not found</returns>
        public static string? GetDepartmentId(ClaimsPrincipal user)
        {
            return user?.FindFirst("departmentId")?.Value;
        }

        /// <summary>
        /// Extracts department name from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>Department name as string, or null if not found</returns>
        public static string? GetDepartmentName(ClaimsPrincipal user)
        {
            return user?.FindFirst("departmentName")?.Value;
        }

        /// <summary>
        /// Extracts user role from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>User role as string, or null if not found</returns>
        public static string? GetUserRole(ClaimsPrincipal user)
        {
            return user?.FindFirst(ClaimTypes.Role)?.Value;
        }

        /// <summary>
        /// Creates a user info object from JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>UserInfo object with extracted claims</returns>
        public static UserInfo GetUserInfo(ClaimsPrincipal user)
        {
            return new UserInfo
            {
                UserId = GetUserId(user),
                Email = GetUserEmail(user),
                FullName = GetUserFullName(user),
                DepartmentId = GetDepartmentId(user),
                DepartmentName = GetDepartmentName(user),
                Role = GetUserRole(user)
            };
        }

        /// <summary>
        /// Validates that required user information is present in JWT claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from HttpContext.User</param>
        /// <returns>True if all required claims are present</returns>
        public static bool HasRequiredClaims(ClaimsPrincipal user)
        {
            return !string.IsNullOrEmpty(GetUserId(user)) &&
                   !string.IsNullOrEmpty(GetUserEmail(user)) &&
                   !string.IsNullOrEmpty(GetDepartmentId(user));
        }
    }

    /// <summary>
    /// Data transfer object for user information extracted from JWT
    /// </summary>
    public class UserInfo
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? Role { get; set; }
    }
}
