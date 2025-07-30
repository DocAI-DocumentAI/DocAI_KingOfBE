

namespace ChatBox.Domain.Models
{
    /// <summary>
    /// Represents a chat session/conversation between user and AI
    /// </summary>
    public class ChatSession : BaseEntity
    {
        public string Title { get; set; }
        public string UserId { get; set; }
        public string ModelName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<SessionPreference> Preferences { get; set; } = new List<SessionPreference>();

    }
}
