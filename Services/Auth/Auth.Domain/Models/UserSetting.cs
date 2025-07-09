using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Auth.Domain.Models;

public class UserSetting
{
    [Key]
    public Guid Id { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }
    public bool NotificationsEnabled { get; set; }
    public DateTime UpdateAt { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; }
}