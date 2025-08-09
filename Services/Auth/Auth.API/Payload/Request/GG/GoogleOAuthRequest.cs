namespace Auth.API.Payload.Request.GG;

public class GoogleOAuthRequest
{
    public string Code { get; set; }
    public string State { get; set; }
}