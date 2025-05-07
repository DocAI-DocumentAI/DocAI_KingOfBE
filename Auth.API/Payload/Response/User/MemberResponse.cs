using Auth.API.Models;

namespace Auth.API.Payload.Response;

public class MemberResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ProvinceCode { get; set; }
    public string? DistrictCode { get; set; }
    public string? CityCode { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime UpdateAt { get; set; }
}