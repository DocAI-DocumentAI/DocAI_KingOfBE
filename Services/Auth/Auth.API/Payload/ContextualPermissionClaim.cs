namespace Auth.API.Payload;

public class ContextualPermissionClaim
{
    public Guid DeptId { get; set; }
    public string DeptName { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; }
    public Guid PermissionId { get; set; }
    public string PermissionName { get; set; }
}