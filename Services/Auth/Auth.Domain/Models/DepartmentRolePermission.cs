using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class DepartmentRolePermission
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; }
    public Guid PermissionId { get; set; }
    [ForeignKey("PermissionId")]
    public Permission Permission { get; set; }
    public Guid DepartmentId { get; set; }
    [ForeignKey("DepartmentId")]
    public Department Department { get; set; }
    public Guid RoleId { get; set; }
    [ForeignKey("RoleId")]
    public Role Role { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
}