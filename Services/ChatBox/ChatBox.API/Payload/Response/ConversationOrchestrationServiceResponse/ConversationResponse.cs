namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class ConversationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Response { get; set; }
        public Guid MessageId { get; set; }
        public Guid SessionId { get; set; }
        public List<DocumentReference> DocumentReferences { get; set; } = new();
        public List<string> SuggestedQuestions { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public int TokensUsed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
