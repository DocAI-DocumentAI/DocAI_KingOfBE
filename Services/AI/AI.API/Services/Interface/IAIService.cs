using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using OllamaSharp.Models;

namespace AI.API.Services.Interface
{
    public interface IAIService
    {
        // Core generation methods
        Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<StreamChunk> StreamGenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default);

        // Enhanced generation with external context (from ChatBox)
        Task<AIResponse> GenerateWithContextAsync(AIContextRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<StreamChunk> StreamWithContextAsync(AIContextRequest request, CancellationToken cancellationToken = default);

        // Model management
        Task<AIResponse> GenerateWithModelAsync(string modelId, AIRequest request, CancellationToken cancellationToken = default);
        Task<List<AIModel>> GetAvailableModelsAsync();
        Task<ModelCapabilities> GetModelCapabilitiesAsync(string modelId);

        // Utility functions
        Task<TokenCountResult> CountTokensAsync(string text, string? model = null);
        Task<IntentResult> DetectIntentAsync(string text);
        Task<string> SuggestTitleAsync(string content);

        // Embedding generation
        Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
        Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync(BatchEmbeddingRequest request, CancellationToken cancellationToken = default);

        // Model validation
        Task<bool> ValidateModelAvailabilityAsync(string modelType, CancellationToken cancellationToken = default);
    }
}
