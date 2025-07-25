using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChatBox.Domain.Enum;

namespace ChatBox.Domain.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public string AiResponse { get; set; }
        public MessageType MessageType { get; set; }
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SourceDocuments { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletionReason { get; set; }
        public string Metadata { get; set; }
    }
}
