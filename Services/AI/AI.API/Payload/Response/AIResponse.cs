using AI.Domain.Models;

namespace AI.API.Payload.Response
{
    public class AIResponse : BaseResponse
    {
        public string Answer { get; set; }
        public List<Document> SourceDocuments { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class StreamChunk
    {
        public string Content { get; set; }
        public bool IsComplete { get; set; }
        public int? TokenCount { get; set; }
    }
}
