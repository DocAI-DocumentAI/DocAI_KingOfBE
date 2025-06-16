

using Auth.Domain.Models;

namespace Auth.API.Payload.Response;

public class DepartmentResponse
{
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
}