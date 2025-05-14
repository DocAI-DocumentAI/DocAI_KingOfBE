using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request;

public class LoginRequest
{
    [Required]
    public string Username { get; set; }
    [Required]
    public string Password { get; set; }
}