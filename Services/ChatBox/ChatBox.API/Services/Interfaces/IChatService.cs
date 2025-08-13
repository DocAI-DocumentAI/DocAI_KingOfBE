using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Implement;

namespace ChatBox.API.Services.Interfaces
{
    public interface IChatService
    {
        Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId);
        Task<IAsyncEnumerable<ChatStreamResponse>> SendMessageStreamAsync(ChatRequest request, string userId, CancellationToken cancellationToken = default);
        Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string userId);
        Task<SessionDetailResponse> GetSessionAsync(string sessionId, string userId);
        Task<List<SessionResponse>> GetUserSessionsAsync(string userId);
        Task<bool> DeleteSessionAsync(string sessionId, string userId);
        Task<ApiResponse<object>> ValidateMessageAsync(string message);
        Task<List<AvailableModelResponse>> GetAvailableModelsAsync();
        Task<bool> SwitchSessionModelAsync(string sessionId, string newModelName, string userId);

        Task<bool> VerifyMessagesInDatabase(string sessionId);

    }
}
