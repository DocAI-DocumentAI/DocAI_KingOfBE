using System.Text.Json.Serialization;
using Notification.Domain.Enums;

namespace Notification.API.Payload.Response;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string? DocumentId { get; set; }
    public string? DocumentVersion { get; set; }
    public NotificationType NotificationType { get; set; }
    public RecipientType RecipientType { get; set; }
    public string RecipientAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public bool IsRead { get; set; } = false;     // ✅ Cần thêm
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreateAt { get; set; }
}