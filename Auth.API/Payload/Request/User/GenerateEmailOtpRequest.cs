using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request;

public class GenerateEmailOtpRequest
{
    [Required]
    public string Email { get; set; }
}
