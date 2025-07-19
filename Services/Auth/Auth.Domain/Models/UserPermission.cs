using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class UserPermission
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; }
    public Guid PermissionId { get; set; }
    [ForeignKey("PermissionId")]
    public Permission Permission { get; set; }
}