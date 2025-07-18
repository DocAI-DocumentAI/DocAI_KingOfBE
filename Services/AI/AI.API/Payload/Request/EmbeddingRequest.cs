namespace AI.API.Payload.Request
{
    public class EmbeddingRequest
    {
        public string DocumentId { get; set; } = string.Empty; 
        public string Content { get; set; } = string.Empty;
        public string? Title { get; set; } 
        public Dictionary<string, object>? Metadata { get; set; } 
    }
}
