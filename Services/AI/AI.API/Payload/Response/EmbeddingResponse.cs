namespace AI.API.Payload.Response
{
    public class EmbeddingResponse
    {
        public string DocumentId { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Dimensions { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string RequestId { get; set; } = string.Empty;
    }
}
