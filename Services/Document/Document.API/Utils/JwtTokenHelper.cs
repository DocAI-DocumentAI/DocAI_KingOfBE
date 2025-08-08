using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Document.API.Utils
{
    /// <summary>
    /// Centralized utility class for extracting user information from JWT tokens
    /// Provides consistent error handling and claim extraction across the Document service
    /// </summary>
    public static class JwtTokenHelper
    {
        #region Core Claim Extraction Methods

        /// <summary>
        /// Extracts user ID from JWT token with strict validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User ID as string</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or user ID claim is missing</exception>
        public static string GetUserId(IHttpContextAccessor httpContextAccessor)
        {
            var user = GetAuthenticatedUser(httpContextAccessor);
            var userIdClaim = user.FindFirst("userId")?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found in JWT token");
            }
            
            return userIdClaim;
        }

        /// <summary>
        /// Extracts user ID from JWT token with optional validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User ID as string or null if not found</returns>
        public static string? GetUserIdOrNull(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                return user?.FindFirst("userId")?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts department ID from JWT token with strict validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>Department ID as string</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or department ID claim is missing</exception>
        public static string GetDepartmentId(IHttpContextAccessor httpContextAccessor)
        {
            var user = GetAuthenticatedUser(httpContextAccessor);
            
            // Try both claim names for backward compatibility
            var departmentIdClaim = user.FindFirst("departmentId")?.Value ?? user.FindFirst("departmentID")?.Value;
            
            if (string.IsNullOrEmpty(departmentIdClaim))
            {
                throw new UnauthorizedAccessException("Department ID not found in JWT token");
            }
            
            return departmentIdClaim;
        }

        /// <summary>
        /// Extracts department ID from JWT token with optional validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>Department ID as string or null if not found</returns>
        public static string? GetDepartmentIdOrNull(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                // Try both claim names for backward compatibility
                return user?.FindFirst("departmentId")?.Value ?? user?.FindFirst("departmentID")?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts user email from JWT token with strict validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User email as string</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated or email claim is missing</exception>
        public static string GetUserEmail(IHttpContextAccessor httpContextAccessor)
        {
            var user = GetAuthenticatedUser(httpContextAccessor);

            // Try multiple possible email claim names
            var emailClaim = user.FindFirst("email")?.Value ??
                           user.FindFirst(ClaimTypes.Email)?.Value ??
                           user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;

            if (string.IsNullOrEmpty(emailClaim))
            {
                throw new UnauthorizedAccessException("User email not found in JWT token");
            }

            return emailClaim;
        }

        /// <summary>
        /// Extracts user email from JWT token with optional validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User email as string or null if not found</returns>
        public static string? GetUserEmailOrNull(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                // Try multiple possible email claim names
                return user?.FindFirst("email")?.Value ??
                       user?.FindFirst(ClaimTypes.Email)?.Value ??
                       user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts user role from JWT token
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User role as string or empty string if not found</returns>
        public static string GetUserRole(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                return user?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts user full name from JWT token
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>User full name as string or null if not found</returns>
        public static string? GetUserFullName(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                // Try multiple possible name claim names
                return user?.FindFirst("fullName")?.Value ??
                       user?.FindFirst(ClaimTypes.Name)?.Value ??
                       user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts department name from JWT token
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor to get current user</param>
        /// <returns>Department name as string or null if not found</returns>
        public static string? GetDepartmentName(IHttpContextAccessor httpContextAccessor)
        {
            try
            {
                var user = GetUserSafely(httpContextAccessor);
                return user?.FindFirst("departmentName")?.Value;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the current authenticated user with strict validation
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <returns>ClaimsPrincipal representing the authenticated user</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated</exception>
        private static ClaimsPrincipal GetAuthenticatedUser(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor?.HttpContext?.User;

            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            return user;
        }

        /// <summary>
        /// Gets the current user safely without throwing exceptions
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <returns>ClaimsPrincipal or null if not available</returns>
        private static ClaimsPrincipal? GetUserSafely(IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor?.HttpContext?.User;
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Creates a user info object with all available claims
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <returns>UserInfo object with extracted claims</returns>
        public static UserInfo GetUserInfo(IHttpContextAccessor httpContextAccessor)
        {
            return new UserInfo
            {
                UserId = GetUserIdOrNull(httpContextAccessor),
                Email = GetUserEmailOrNull(httpContextAccessor),
                FullName = GetUserFullName(httpContextAccessor),
                DepartmentId = GetDepartmentIdOrNull(httpContextAccessor),
                DepartmentName = GetDepartmentName(httpContextAccessor),
                Role = GetUserRole(httpContextAccessor)
            };
        }

        /// <summary>
        /// Checks if the current user is authenticated
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <returns>True if user is authenticated, false otherwise</returns>
        public static bool IsAuthenticated(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor?.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true;
        }

        #endregion
    }

    /// <summary>
    /// Data transfer object for user information extracted from JWT token
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
