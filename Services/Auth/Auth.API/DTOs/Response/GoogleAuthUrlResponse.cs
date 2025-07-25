namespace Auth.API.DTOs.Response;

public class GoogleAuthUrlResponse
{
    public string AuthUrl { get; set; }
    public string State { get; set; }
}