using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Permission;
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.UserSetting;

namespace Auth.API.Payload.Response.User;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string FullName { get; set; }
    public bool Active { get; set; }
    public RoleResponse Role { get; set; }
    public List<PermissionResponse> Permissions { get; set; }
    public DepartmentResponse Department { get; set; }
    public UserSettingResponse UserSetting { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
}