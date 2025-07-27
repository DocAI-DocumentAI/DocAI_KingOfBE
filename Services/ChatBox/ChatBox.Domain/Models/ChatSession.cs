using System.ComponentModel.DataAnnotations;
using ChatBox.Domain.Enum;

namespace ChatBox.Domain.Models
{
    /// <summary>
    /// Represents a chat session/conversation between user and AI
    /// </summary>
    public class ChatSession 
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int MessageCount { get; set; }
        public string AIModelId { get; set; }
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 2000;
        public string InitialContext { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletionReason { get; set; }

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    }
}
