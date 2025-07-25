using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAiServiceClient
    {
        // Core Text Generation
        Task<AiGenerationResult> GenerateResponseAsync(AdvancedAiGenerationRequest request);
        Task<IAsyncEnumerable<StreamingChunk>> StreamResponseAsync(StreamingRequest request);

        // Token Management
        Task<int> CountTokensAsync(string text, string model = "default");
        Task<TokenBreakdown> EstimateFullTokenUsageAsync(EstimateTokenRequest request);
        Task<string> TruncateToTokenLimitAsync(string text, int maxTokens);

        // Content Analysis
        Task<MessageAnalysisResult> AnalyzeContentAsync(ContentAnalysisRequest request);
        Task<string> DetectLanguageAsync(string content);

        // Conversation Features
        Task<ConversationSummaryResult> GenerateConversationSummaryAsync(ConversationSummaryRequest request);

        // Smart Features
        Task<IntentDetectionResult> DetectIntentAsync(IntentDetectionRequest request);
        Task<string> SuggestTitleAsync(TitleSuggestionRequest request);

        // Text Processing
        Task<string> TranslateTextAsync(TranslationRequest request);
        Task<string> SummarizeTextAsync(string text, int maxLength = 200);

        // Health & Models
        Task<List<AvailableModel>> GetAvailableModelsAsync();
    }
}
