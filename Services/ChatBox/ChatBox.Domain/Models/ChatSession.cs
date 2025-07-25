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
        public SessionStatus Status { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public string SessionType { get; set; }
        public string InitialContext { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletionReason { get; set; }

    }
}
