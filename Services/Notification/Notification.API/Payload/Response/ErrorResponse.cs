namespace Notification.API.Payload.Response;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = "An error occurred.";
    public string? Details { get; set; }
    public string TraceId { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}