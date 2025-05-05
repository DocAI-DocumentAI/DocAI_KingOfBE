using Auth.API.Enums;
namespace Auth.API.Payload.Response;

public class LoginResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public RoleEnum Role { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}