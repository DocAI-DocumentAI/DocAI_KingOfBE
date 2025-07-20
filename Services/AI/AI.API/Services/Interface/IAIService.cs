using AI.API.Payload.Request;
using AI.API.Payload.Response;

namespace AI.API.Services.Interface
{
    public interface IAIService
    {
        Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default);
        IAsyncEnumerable<StreamChunk> StreamGenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default);
        Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
        Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync(BatchEmbeddingRequest request, CancellationToken cancellationToken = default);
        Task<bool> ValidateModelAvailabilityAsync(string modelType, CancellationToken cancellationToken = default);
    }
}
