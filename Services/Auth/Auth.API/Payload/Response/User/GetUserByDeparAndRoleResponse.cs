using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;

public class GetUserByDeparAndRoleResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public RoleResponse Role { get; set; }
    public DepartmentResponse Department { get; set; }
}