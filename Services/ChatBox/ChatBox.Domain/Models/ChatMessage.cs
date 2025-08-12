

using ChatBox.Domain.Enum;

namespace ChatBox.Domain.Models
{
    public class ChatMessage : BaseEntity
    {
        public string Content { get; set; }
        public MessageRole Role { get; set; }
        public int TokenCount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SessionId { get; set; }
        public string DocumentSources { get; set; }
        public virtual ChatSession Session { get; set; }
    }
}
