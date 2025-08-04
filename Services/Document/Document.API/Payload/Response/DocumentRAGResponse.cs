namespace Document.API.Payload.Response
{
    public class DocumentRAGResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Answer { get; set; } = string.Empty;
        public List<DocumentSourceResponse> Sources { get; set; } = new();
        public string QueryProcessed { get; set; } = string.Empty;
        public long ProcessingTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
