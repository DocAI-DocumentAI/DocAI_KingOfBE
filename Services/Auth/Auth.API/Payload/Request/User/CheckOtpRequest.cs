using System.ComponentModel.DataAnnotations;

namespace Auth.API.Payload.Request.User;

public class CheckOtpRequest
{
    [Required]
    public string Email { get; set; }
    
    [Required]
    public string Otp { get; set; }
    
    /// <summary>
    /// Có xóa OTP sau khi validate thành công không
    /// </summary>
    public bool RemoveAfterValidation { get; set; } = false;
}