using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class Role
{
    [Key]
    public Guid Id { get; set; }
    public string RoleName { get; set; }
    public string Description { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
    public virtual ICollection<RolePermission>? RolePermissions { get; set; }
}