using System.ComponentModel.DataAnnotations;
using Auth.API.Enums;

namespace Auth.API.Models;

public class User
{
    [Key]
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string FullName { get; set; }
    public RoleEnum Role { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
}