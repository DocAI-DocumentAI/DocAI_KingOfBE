using AI.API.Payload.Request;
using AI.API.Payload.Response;

namespace AI.API.Services.Interface
{
    public interface IOllamaAIService
    {
        Task<AIResponse> GenerateResponseAsync(AIRequest request); // Cho non-streaming response
        IAsyncEnumerable<string> StreamGenerateResponseAsync(AIRequest request); // Cho streaming response

        // REVIEW POINT: Thêm phương thức mới cho Embedding Generation
        Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request);
    }
}
