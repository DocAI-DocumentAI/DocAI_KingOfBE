using System;
using Auth.API.Payload.Response.Staff;
using Auth.Domain.Enums;
using Auth.Domain.Models;

namespace Auth.API.Payload.Response;

public class LoginResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public ICollection<RoleResponse> Roles { get; set; }
    public ICollection<DepartmentResponse> Departments { get; set; }

    public string Token { get; set; }
    public string RefreshToken { get; set; }
}