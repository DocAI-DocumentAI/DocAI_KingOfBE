using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;
using ChatBox.Infrastructure.Paginate;

namespace ChatBox.API.Services.Interfaces;

    public interface IChatService
{
    // Core Messaging
    Task<SendMessageResponse> SendMessageAsync(Guid userId, SendMessageRequest request, string ipAddress, string userAgent);
    Task<StreamingResponse> StartStreamingAsync(Guid userId, StreamChatRequest request, string connectionId);
    Task<bool> CancelStreamingAsync(Guid userId, Guid messageId);

    // Message Management
    Task<AdvancedMessageResponse> GetMessageAsync(Guid userId, Guid messageId);
    Task<bool> DeleteMessageAsync(Guid userId, Guid messageId, string reason = "user_request");
    Task<bool> AddFeedbackAsync(Guid userId, FeedbackRequest request);

    // Session Management
    Task<AdvancedSessionResponse> CreateSessionAsync(Guid userId, CreateSessionRequest request, string ipAddress, string userAgent);
    Task<AdvancedSessionResponse> GetSessionAsync(Guid userId, Guid sessionId);
    Task<IPaginate<SessionSummaryResponse>> GetSessionsAsync(Guid userId, GetSessionsRequest request);
    Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, string reason = "user_request");

    // Advanced Features
    Task<ConversationSummaryResponse> GenerateSummaryAsync(Guid userId, Guid sessionId);

    // Search & Discovery
    Task<IPaginate<SearchResult>> SearchConversationsAsync(Guid userId, SearchRequest request);



    // Health & Monitoring
    Task<List<AlertResponse>> GetUserAlertsAsync(Guid userId);
}
