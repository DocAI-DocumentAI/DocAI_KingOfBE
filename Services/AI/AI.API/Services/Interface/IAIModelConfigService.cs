using AI.Domain.Models;
using AI.API.Payload.Request;
using AI.API.Payload.Response;

namespace AI.API.Services.Interface
{
    public interface IAIModelConfigService
    {
        Task<List<ModelConfigDto>> GetAllModelsAsync();
        Task<ModelConfigDto?> GetModelByIdAsync(int id);
        Task<bool> UpdateModelAsync(int id, UpdateAIModelConfigRequest request);
        Task<TestModelResponse> TestModelAsync(int id);
        Task<bool> ActivateModelAsync(int id);
        Task<List<ProviderInfo>> GetSupportedProvidersAsync();
    }


    public class TestModelResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long ResponseTimeMs { get; set; }
        public string? Response { get; set; }
        public string? Error { get; set; }
    }

    public class ProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string DefaultEndpoint { get; set; } = string.Empty;
        public bool RequiresApiKey { get; set; } = true;
        public bool RequiresEndpoint { get; set; } = false;
    }
}
