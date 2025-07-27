using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAiServiceClient
    {
        Task<AiGenerationResult> GenerateResponseAsync(AdvancedAiGenerationRequest request);
        Task<IAsyncEnumerable<StreamingChunk>> StreamResponseAsync(StreamingRequest request);
        Task<int> CountTokensAsync(string text, string model = "default");
        Task<string> TruncateToTokenLimitAsync(string text, int maxTokens);
        Task<IntentDetectionResult> DetectIntentAsync(IntentDetectionRequest request);
        Task<string> SuggestTitleAsync(TitleSuggestionRequest request);
        //Task<List<AIModelInfo>> GetAvailableModelsAsync();

    }
}
