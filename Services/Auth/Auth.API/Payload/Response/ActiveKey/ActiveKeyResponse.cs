using Auth.Domain.Models;

namespace Auth.API.Payload.Response.ActiveKey;

public class ActiveKeyResponse
{
    public Guid Id { get; set; }
    public string ActivationCode { get; set; }
    public string Status { get; set; }
    public Role Role { get; set; }
    public Department Department { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}