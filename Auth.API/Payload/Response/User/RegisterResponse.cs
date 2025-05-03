namespace Auth.API.Payload.Response;

public class RegisterResponse
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string FullName { get; set; }
    public DateTime CreatAt { get; set; }
    public DateTime UpdateAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string TwoFactorMethod { get; set; }
    public string Token { get; set; } 
    public string RefreshToken { get; set; } 
}