namespace Auth.API.Payload;

public class ContextualPermissionClaim
{
    public Guid DeptId { get; set; }
    public string DeptName { get; set; }
    public string RoleName { get; set; }
    public string PermissionName { get; set; }
}