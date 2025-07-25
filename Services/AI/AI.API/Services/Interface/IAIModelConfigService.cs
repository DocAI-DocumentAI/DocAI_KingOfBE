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

    public class ModelConfigDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public AIProviderType ProviderType { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiVersion { get; set; }
        public string Description { get; set; } = string.Empty;
        public double? AverageResponseTime { get; set; }
        public bool IsEnabled { get; set; }
        public bool HasApiKey { get; set; }
        public bool IsTestedSuccessfully { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string? LastTestError { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
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
