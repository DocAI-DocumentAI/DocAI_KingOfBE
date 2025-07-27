using Azure.AI.Inference;

namespace AI.API.Payload.Response
{
    public class BatchEmbeddingResponse
    {
        public bool Success { get; set; }
        public string RequestId { get; set; }
        public List<EmbeddingResult> Results { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalTimeMs { get; set; }
        public string? Message { get; set; }
    }
}
