namespace AI.API.Payload.Response
{
    public class BatchEmbeddingResponse
    {
        public List<EmbeddingResponse> Results { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string RequestId { get; set; } = string.Empty;
    }
}
