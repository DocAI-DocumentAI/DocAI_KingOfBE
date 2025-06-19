using System;
using Auth.API.Payload.Response.Staff;

namespace Auth.API.Payload.Response;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; } // Thêm FullName

    // Tùy chọn: Để hiển thị ở Frontend
    public List<RoleResponse> Roles { get; set; }
    public List<DepartmentResponse> Departments { get; set; }

    // THÔNG TIN CHÍNH SẼ ĐƯA VÀO JWT
    public List<ContextualPermissionClaim> ContextualPermissions { get; set; }
    public List<string> GeneralRoles { get; set; } // Ví dụ: Admin

    public string Token { get; set; }
    public string RefreshToken { get; set; }
}