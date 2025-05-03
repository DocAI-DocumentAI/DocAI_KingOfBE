using System.Text.Json;

namespace Auth.API.Payload.Response;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Error { get; set; }
    public DateTime TimeStamp { get; set; }
    public string Path { get; set; }
    public string TraceId { get; set; }
}
