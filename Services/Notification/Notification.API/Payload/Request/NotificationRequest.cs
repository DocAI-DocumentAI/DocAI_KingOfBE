using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Notification.API.Payload.Request;

public class NotificationRequest
{
    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "size")]
    public int Size { get; set; } = 10;

    [FromQuery(Name = "documentId")]
    public Guid? DocumentId { get; set; }

    [FromQuery(Name = "notificationType")]
    public string? NotificationType { get; set; }

    [FromQuery(Name = "recipient")]
    public string? Recipient { get; set; }

    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; set; } = "CreateAt";

    [FromQuery(Name = "isAsc")]
    public bool IsAsc { get; set; } = false; // Default sort by newest
}