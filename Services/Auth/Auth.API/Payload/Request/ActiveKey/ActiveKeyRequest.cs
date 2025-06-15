using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.ActiveKey;

public class ActiveKeyRequest
{
    [Required]
    public Guid RoleId { get; set; }
}