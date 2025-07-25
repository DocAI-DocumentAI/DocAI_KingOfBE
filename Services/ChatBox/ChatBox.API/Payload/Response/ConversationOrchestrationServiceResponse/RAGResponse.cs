namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class RAGResponse
    {
        public bool Success { get; set; }
        public string GeneratedResponse { get; set; }
        public List<DocumentReference> SourceDocuments { get; set; } = new();
        public string Context { get; set; }
        public int TokensUsed { get; set; }
        public string Model { get; set; }
        public double ConfidenceScore { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
