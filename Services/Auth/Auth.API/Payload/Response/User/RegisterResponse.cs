using System;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.UserSetting;
using Auth.API.Payload.Response.Permission;

namespace Auth.API.Payload.Response;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public RoleResponse Role { get; set; }
    public DepartmentResponse Department { get; set; }
    public UserSettingResponse UserSetting { get; set; }
    public List<PermissionResponse> Permissions { get; set; } = new();
    public string DocaiToken { get; set; }
    public string DocaiRefreshToken { get; set; }
    public string? GoogleAccessToken { get; set; }
    public string? GoogleRefreshToken { get; set; }
}
