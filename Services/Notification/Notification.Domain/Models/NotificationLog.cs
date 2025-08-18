using System.ComponentModel.DataAnnotations;
using Notification.Domain.Enums;

namespace Notification.Domain.Models;

public class NotificationLog : BaseEntity
{
    [Required]
    public string DocumentId { get; set; } = string.Empty;

    public string? DocumentVersion { get; set; }

    [Required]
    public NotificationType NotificationType { get; set; }

    [Required]
    public RecipientType RecipientType { get; set; }

    [MaxLength(255)]
    public string? RecipientAddress { get; set; }

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public bool IsSent { get; set; }

    public DateTime? SentAt { get; set; }

    public string? ErrorMessage { get; set; }

}