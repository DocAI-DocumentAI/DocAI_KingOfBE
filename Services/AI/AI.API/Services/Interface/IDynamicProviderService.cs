using AI.Domain.Enums;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Interface
{
    public interface IDynamicProviderService
    {
        Task<ITextGenerationService> CreateTextGenerationServiceAsync(string modelId, string apiKey);
        Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingServiceAsync(ModelType modelType);
        Task<bool> ValidateModelConnectionAsync(string modelId, string apiKey, string endpoint);
    }
}
