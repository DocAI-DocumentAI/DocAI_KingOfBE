using System.Text.Json.Serialization;
using Notification.Domain.Enums;

namespace Notification.API.Payload.Response;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string? DocumentVersion { get; set; }
    public NotificationType NotificationType { get; set; }
    public RecipientType RecipientType { get; set; }
    public string RecipientAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime? DismissedAt { get; set; }
    public Guid? DismissedByUserId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreateAt { get; set; }
}