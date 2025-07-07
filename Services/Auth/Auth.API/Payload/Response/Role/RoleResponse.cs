using Auth.Domain.Models;

namespace Auth.API.Payload.Response.Role;

public class RoleResponse
{
    public Guid Id { get; set; }
    public string RoleName { get; set; }
    public string Description { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
}