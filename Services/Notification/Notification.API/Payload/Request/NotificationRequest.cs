using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Notification.API.Payload.Request;

public class NotificationRequest
{
    public int Page { get; set; } = 1;

    public int Size { get; set; } = 10;

    public Guid? DocumentId { get; set; }

    public string? NotificationType { get; set; }

    public string? Recipient { get; set; }

    public string? SortBy { get; set; } = "CreateAt";

    public bool IsAsc { get; set; } = false; // Default sort by newest
}