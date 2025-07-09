using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    public bool Active { get; set; }
    public Guid RoleId { get; set; }
    [ForeignKey("RoleId")]
    public virtual Role Role { get; set; }
    public Guid DepartmentId { get; set; }
    [ForeignKey("DepartmentId")]
    public virtual Department Department { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
}
