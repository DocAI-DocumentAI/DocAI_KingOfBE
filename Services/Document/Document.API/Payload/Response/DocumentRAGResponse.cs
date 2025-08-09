namespace Document.API.Payload.Response
{
    public class DocumentRAGResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string RawContent { get; set; } = string.Empty;

        public List<DocumentSourceResponse> Sources { get; set; } = new();
        public string QueryProcessed { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public long ProcessingTimeMs { get; set; }

    }
}
