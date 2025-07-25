using System.Security.Claims;

namespace AI.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            // Try custom userId claim first
            var userIdClaim = principal.FindFirst("userId");

            // If not found, try the standard NameIdentifier claim
            if (userIdClaim == null)
            {
                userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            }

            // Try "sub" claim (common in JWT tokens)
            if (userIdClaim == null)
            {
                userIdClaim = principal.FindFirst("sub");
            }

            if (string.IsNullOrEmpty(userIdClaim?.Value))
            {
                throw new InvalidOperationException("User ID claim ('userId', 'NameIdentifier', or 'sub') not found in token.");
            }

            return userIdClaim.Value;
        }

        public static string GetUserEmail(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            var emailClaim = principal.FindFirst(ClaimTypes.Email) ?? principal.FindFirst("email");
            return emailClaim?.Value;
        }

        public static string GetUserName(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
            return nameClaim?.Value;
        }

        public static IEnumerable<string> GetUserRoles(this ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }

        public static bool HasRole(this ClaimsPrincipal principal, string role)
        {
            if (principal == null || string.IsNullOrEmpty(role))
            {
                return false;
            }

            return principal.IsInRole(role);
        }
    }
}
