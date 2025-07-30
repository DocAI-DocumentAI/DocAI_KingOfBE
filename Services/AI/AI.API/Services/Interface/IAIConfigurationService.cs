using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Models;

namespace AI.API.Services.Interface
{
    public interface IAIConfigurationService
    {
        Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default);
        Task<string> GetConfigurationAsync(string key);
        Task<List<ConfigurationResponse>> GetAllConfigurationsAsync(string category = null);
        Task SetConfigurationAsync(string key, string value, string category = null, string description = null);
        Task<AIModelConfig> GetActiveAIModelAsync();
        Task<AIModelConfigResponse> GetActiveTextGenerationConfigAsync();
        Task<AIModelConfigResponse> SetTextGenerationConfigAsync(SetAIModelConfigRequest request);
        Task<bool> TestTextGenerationConfigAsync(string modelId, string apiKey);
        Task<AIModelConfigResponse> GetCurrentConfigAsync();
        Task<bool> DeactivateCurrentConfigAsync();
    }
}
