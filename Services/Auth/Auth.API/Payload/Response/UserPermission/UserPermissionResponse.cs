namespace Auth.API.Payload.Response.UserPermission;

public class UserPermissionResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
    public string PermissionName { get; set; }
}