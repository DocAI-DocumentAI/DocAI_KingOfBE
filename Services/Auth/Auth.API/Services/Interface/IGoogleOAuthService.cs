using System.Threading.Tasks;
using Auth.API.DTOs.Request;
using Auth.API.DTOs.Response;
using Auth.API.Payload.Response;

namespace Auth.API.Services.Interface;

public interface IGoogleOAuthService
{
    Task<GoogleTokenValidationResult> ValidateGoogleTokenAsync(string googleToken);
    Task<(string accessToken, string refreshToken)?> RefreshGoogleTokenAsync(string refreshToken);
    Task<GoogleAuthUrlResponse> GetAuthUrlAsync();
    Task<GoogleOAuthResponse> AuthenticateWithGoogleAsync(GoogleOAuthRequest request);
    Task<bool> RevokeGoogleTokenAsync(string userId);
    string GetGoogleAuthUrl(string state = null);
    Task<string> HandleGoogleCallbackAndGenerateRedirectUrlAsync(GoogleOAuthRequest request);
    Task<LoginResponse> ExchangeCodeForLoginResponseAsync(string code);
}

public class GoogleTokenValidationResult
{
    public bool IsValid { get; set; }
    public string Email { get; set; }
    public string GoogleId { get; set; }
    public string Name { get; set; }
    public string Picture { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}


