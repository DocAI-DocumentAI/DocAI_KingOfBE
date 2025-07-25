using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SessionResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public Domain.Enum.SessionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime? ArchivedAt { get; set; }

        // AI Configuration
        public string SystemPrompt { get; set; }
        public int Temperature { get; set; }
        public string ChatMode { get; set; }
        public int MaxTokens { get; set; }
        public bool EnableDocumentSearch { get; set; }

        // Metadata & Tracking
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();

        // Statistics
        public int MessageCount { get; set; }
        public int TokensUsed { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public float AverageRating { get; set; }

        // Security & Compliance
        public bool IsModerated { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ScheduledDeletionAt { get; set; }
    }
}
