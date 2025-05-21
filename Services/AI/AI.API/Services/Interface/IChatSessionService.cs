using AI.Domain.Models;

namespace AI.API.Services.Interface
{
    public interface IChatSessionService
    {
        Task<ChatSession> CreateSessionAsync(string userId);
        Task<ChatSession> GetSessionAsync(Guid sessionId);
        Task<List<ChatSession>> GetUserSessionsAsync(string userId);
        Task<ChatMessage> AddMessageAsync(Guid sessionId, string role, string content, float[] embedding = null);
        Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId);
    }
}
