using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;

namespace Auth.API.Payload.Response.User;

public class UserResponse
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string FullName { get; set; }
    public RoleResponse Role { get; set; }
    public DepartmentResponse Department { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
}