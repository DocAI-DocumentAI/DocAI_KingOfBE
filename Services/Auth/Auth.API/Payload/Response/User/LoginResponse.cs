using System;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.UserSetting;
using Auth.Domain.Enums;
using Auth.Domain.Models;

namespace Auth.API.Payload.Response;

public class LoginResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public RoleResponse Role { get; set; }
    public DepartmentResponse Department { get; set; }
    public UserSettingResponse UserSetting { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}
