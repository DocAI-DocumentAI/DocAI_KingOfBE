using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SendMessageResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Guid MessageId { get; set; }
        public Guid SessionId { get; set; }
        public string Response { get; set; }
        public List<DocumentReference> Sources { get; set; } = new();
        public List<string> SuggestedQuestions { get; set; } = new();
        public int TokensUsed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

}
