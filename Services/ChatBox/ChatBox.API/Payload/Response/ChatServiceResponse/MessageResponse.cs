using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.Domain.Enum;
using ChatBox.Domain.Models;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class MessageResponse
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Content { get; set; }
        public string AiResponse { get; set; }
        public MessageType MessageType { get; set; }
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<DocumentReference> Sources { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public MessageFeedBackResponse Feedback { get; set; }
    }
}
