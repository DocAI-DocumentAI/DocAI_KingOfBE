using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Document.API.Models;

public class OpenRouterConfigSetting
{
    public string Model { get; set; }
    public string APIKey { get; set; }
    public string Endpoint { get; set; }
}

public class GeminiConfigSetting
{
    public string TextModel { get; set; }
    public string EmbeddingModel { get; set; }
    public string APIKey { get; set; }
}