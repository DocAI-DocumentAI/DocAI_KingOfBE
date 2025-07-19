using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.UserPermission;

public class AddUserPermissionRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid PermissionId { get; set; }
}