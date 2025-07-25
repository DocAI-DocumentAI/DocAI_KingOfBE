using System.Text.Json.Serialization;

namespace Auth.API.DTOs.Response;

public class GoogleUserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }
}