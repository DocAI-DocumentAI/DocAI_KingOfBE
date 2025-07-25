using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class AdvancedMessageResponse
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Content { get; set; }
        public string Response { get; set; }
        public MessageType Type { get; set; }
        public List<DocumentReference> Sources { get; set; } = new();
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; }
        public FeedbackInfo Feedback { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
