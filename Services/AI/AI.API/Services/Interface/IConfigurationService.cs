using AI.API.Payload.Request;
using AI.API.Payload.Response;

namespace AI.API.Services.Interface
{
    public interface IConfigurationService
    {
        // System Configuration
        Task<T> GetConfigurationAsync<T>(string key, T defaultValue = default);
        Task<ConfigurationResponse> SetConfigurationAsync(UpdateConfigurationRequest request);
        Task<Dictionary<string, string>> GetAllConfigurationsAsync(string category = null);
        Task<bool> DeleteConfigurationAsync(string key);

        // Model Configuration
        Task<ModelConfigurationResponse> GetActiveModelConfigurationAsync(string modelType);
        Task<List<ModelConfigurationResponse>> GetAllModelConfigurationsAsync(bool activeOnly = false);
        Task<ModelConfigurationResponse> CreateModelConfigurationAsync(UpdateModelConfigurationRequest request);
        Task<ModelConfigurationResponse> UpdateModelConfigurationAsync(int id, UpdateModelConfigurationRequest request);
        Task<bool> DeleteModelConfigurationAsync(int id);
        Task<bool> ActivateModelConfigurationAsync(int id);

        // Model Providers
        Task<List<ModelProviderResponse>> GetModelProvidersAsync(bool activeOnly = false);
        Task<bool> TestModelConnectionAsync(int modelConfigId);
    }
}
