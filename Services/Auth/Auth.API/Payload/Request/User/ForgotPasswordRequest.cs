using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.User;

public class ForgotPasswordRequest
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
    [Required]
    public string ConfirmPassword { get; set; }
}