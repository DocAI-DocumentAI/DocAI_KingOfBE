using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.Infrastructure.Paginate;

namespace ChatBox.API.Services.Interfaces;

    public interface IChatService
{
    // Core Messaging
    Task<SendMessageResponse> SendMessageAsync(Guid userId, SendMessageRequest request, string ipAddress, string userAgent);
    Task<StreamingResponse> StartStreamingAsync(Guid userId, StreamChatRequest request, string connectionId);
    Task<bool> CancelStreamingAsync(Guid userId, Guid messageId);

    // Message Management
    Task<MessageResponse> GetMessageAsync(Guid userId, Guid messageId);
    Task<bool> DeleteMessageAsync(Guid userId, Guid messageId, string reason = "user_request");

    // Session Management
    Task<SessionResponse> CreateSessionAsync(Guid userId, CreateSessionRequest request, string ipAddress, string userAgent);
    Task<SessionResponse> GetSessionAsync(Guid userId, Guid sessionId);
    Task<IPaginate<SessionResponse>> GetSessionsAsync(Guid userId, GetSessionsRequest request);
    Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, string reason = "user_request");

    // Search & Discovery
    Task<IPaginate<SearchResult>> SearchConversationsAsync(Guid userId, SearchRequest request);

    //Task<SwitchModelResponse> SwitchAIModelAsync(Guid userId, Guid sessionId, SwitchModelRequest request);
    //Task<List<AIModelInfo>> GetAvailableAIModelsAsync();
    //Task<AIModelInfo> GetAIModelInfoAsync(string modelId);

}
