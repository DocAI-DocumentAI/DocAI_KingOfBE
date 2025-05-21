using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.API.Services.Implement
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly DocAIDbContext _dbContext;
        private readonly IEmbeddingService _embeddingService;

        public ChatSessionService(DocAIDbContext dbContext, IEmbeddingService embeddingService)
        {
            _dbContext = dbContext;
            _embeddingService = embeddingService;
        }

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            return session;
        }

        public async Task<ChatSession> GetSessionAsync(Guid sessionId)
        {
            return await _dbContext.ChatSessions
                .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
        {
            return await _dbContext.ChatSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();
        }

        public async Task<ChatMessage> AddMessageAsync(Guid sessionId, string role, string content, float[] embedding = null)
        {
            var session = await _dbContext.ChatSessions.FindAsync(sessionId)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found");

            session.UpdatedAt = DateTime.UtcNow;

            // Generate embedding if not provided
            if (embedding == null && !string.IsNullOrWhiteSpace(content))
            {
                embedding = await _embeddingService.GetEmbeddingsAsync(content);
            }

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = role,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                Embedding = embedding
            };

            _dbContext.ChatMessages.Add(message);
            await _dbContext.SaveChangesAsync();

            return message;
        }

        public async Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId)
        {
            return await _dbContext.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }
    }
}

