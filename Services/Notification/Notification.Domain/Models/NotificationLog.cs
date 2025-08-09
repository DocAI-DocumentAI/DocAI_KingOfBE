using System.ComponentModel.DataAnnotations;
using Notification.Domain.Enums;

namespace Notification.Domain.Models;

public class NotificationLog : BaseEntity
{
    [Required]
    public string DocumentId { get; set; }

    public string? DocumentVersion { get; set; }

    [Required]
    public NotificationType NotificationType { get; set; }

    [Required]
    public RecipientType RecipientType { get; set; }

    [MaxLength(255)]
    public string? RecipientAddress { get; set; }

    [Required]
    public string Subject { get; set; } = null!;

    [Required]
    public string Message { get; set; } = null!;

    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDismissed { get; set; } = false;
    public DateTime? DismissedAt { get; set; }
    public Guid? DismissedByUserId { get; set; }
    public Guid? DismissToken { get; set; }

}