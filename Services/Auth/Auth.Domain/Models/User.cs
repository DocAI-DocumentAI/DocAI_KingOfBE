using System;
using System.ComponentModel.DataAnnotations;
using Auth.Domain.Enums;

namespace Auth.Domain.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string FullName { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
    public virtual ICollection<UserRole>? UserRoles { get; set; }
    public virtual ICollection<UserDepartment>? UserDepartments { get; set; }
    public virtual ICollection<DepartmentRolePermission>? DepartmentRolePermissions { get; set; }
}