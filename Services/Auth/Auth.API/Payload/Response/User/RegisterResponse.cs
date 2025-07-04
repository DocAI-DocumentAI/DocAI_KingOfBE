using System;
using Auth.API.Payload.Response.Staff;

namespace Auth.API.Payload.Response;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public RoleResponse Role { get; set; }
    public DepartmentResponse Department { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}