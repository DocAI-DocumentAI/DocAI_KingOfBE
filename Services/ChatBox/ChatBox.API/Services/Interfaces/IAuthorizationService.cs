using System.Security.Claims;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAuthorizationService
    {
        /// <summary>
        /// Kiểm tra user có role cụ thể không
        /// </summary>
        bool HasRole(string role);

        /// <summary>
        /// Kiểm tra user có một trong các roles không
        /// </summary>
        bool HasAnyRole(params string[] roles);

        /// <summary>
        /// Kiểm tra user có tất cả các roles không
        /// </summary>
        bool HasAllRoles(params string[] roles);

        /// <summary>
        /// Kiểm tra user thuộc department cụ thể không
        /// </summary>
        bool IsInDepartment(string departmentName);

        /// <summary>
        /// Kiểm tra user thuộc một trong các departments không
        /// </summary>
        bool IsInAnyDepartment(params string[] departmentNames);

        /// <summary>
        /// Kiểm tra user có permission cụ thể không
        /// </summary>
        bool HasPermission(string permission);

        /// <summary>
        /// Kiểm tra user có một trong các permissions không
        /// </summary>
        bool HasAnyPermission(params string[] permissions);

        /// <summary>
        /// Kiểm tra user có tất cả các permissions không
        /// </summary>
        bool HasAllPermissions(params string[] permissions);

        /// <summary>
        /// Lấy thông tin user hiện tại từ JWT
        /// </summary>
        ClaimsPrincipal GetCurrentUser();

        /// <summary>
        /// Lấy UserId từ JWT
        /// </summary>
        Guid GetCurrentUserId();

        /// <summary>
        /// Lấy Role từ JWT
        /// </summary>
        string? GetCurrentUserRole();

        /// <summary>
        /// Lấy Department từ JWT
        /// </summary>
        string? GetCurrentUserDepartment();

        /// <summary>
        /// Lấy danh sách Permissions từ JWT
        /// </summary>
        string[] GetCurrentUserPermissions();

        /// <summary>
        /// Kiểm tra authorization phức tạp với logic AND/OR
        /// </summary>
        bool CheckAuthorization(string[]? roles = null, string[]? departments = null, string[]? permissions = null, bool requireAll = false);
    }
}
