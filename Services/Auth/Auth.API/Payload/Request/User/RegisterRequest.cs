using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request;

public class RegisterRequest
{
    [Required]
    public string Password { get; set; }
    [Required]
    public string Email { get; set; }
    [Required]
    public string Phone { get; set; }
    [Required]
    public string FullName { get; set; }
    // [Required]
    // public string Otp { get; set; }
    [Required]
    public Guid RoleId { get; set; }
    [Required]
    public Guid DepartmentId { get; set; }
    // [Required]
    // public string ActivationCode { get; set; }
}