namespace Auth.API.Payload.Response.Auth;

public class RefreshTokenResponse
{
    public string DocaiToken { get; set; }
    public string DocaiRefreshToken { get; set; }
    public string? GoogleAccessToken { get; set; }
    public string? GoogleRefreshToken { get; set; }
}