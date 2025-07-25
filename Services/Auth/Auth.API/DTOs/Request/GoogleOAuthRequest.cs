namespace Auth.API.DTOs.Request;

public class GoogleOAuthRequest
{
    public string Code { get; set; }
    public string State { get; set; }
}