

using Auth.Domain.Models;

namespace Auth.API.Payload.Response.Department;

public class DepartmentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
}