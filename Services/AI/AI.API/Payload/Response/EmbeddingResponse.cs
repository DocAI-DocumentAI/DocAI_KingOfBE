namespace AI.API.Payload.Response
{
    public class EmbeddingResponse : BaseResponse
    {
        public string DocumentId { get; set; }
        public float[] Embedding { get; set; }
        public int Dimensions { get; set; }
    }

    public class BatchEmbeddingResponse : BaseResponse
    {
        public List<EmbeddingResult> Results { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalTimeMs { get; set; }
    }

    public class EmbeddingResult
    {
        public string DocumentId { get; set; }
        public bool Success { get; set; }
        public float[] Embedding { get; set; }
        public int Dimensions { get; set; }

    }
