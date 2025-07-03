using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces;

    public interface IChatService
{
    Task<ConversationResponse> StartNewConversationAsync(string userId, List<string> userRoles, ChatRequestPayload requestPayload);
    Task<List<ConversationSummaryResponse>> GetUserConversationsAsync(string userId);
    Task<List<MessageResponse>> GetConversationHistoryAsync(string conversationId, string userId);
    Task<ChatResponse> ContinueChatAsync(string conversationId, string userQuestion, string userId, List<string> userRoles); // REVIEW POINT: Thêm userRoles
    IAsyncEnumerable<string> StreamContinueChatAsync(string conversationId, string userQuestion, string userId, List<string> userRoles); // REVIEW POINT: Thêm userRoles
    Task<bool> DeleteConversationAsync(string conversationId, string userId); 
}
