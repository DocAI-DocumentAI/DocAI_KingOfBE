using AI.Domain.Models;

namespace AI.API.Payload.Response
{
    public class AIResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<Document> SourceDocuments { get; set; } = new List<Document>();
        public int TokensUsed { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
