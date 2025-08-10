using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.Auth;

public class ResetPasswordByEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
}