namespace AI.API.Payload.Response
{
    public class EmbeddingResponse
    {
        public bool Success { get; set; }
        public string RequestId { get; set; }
        public string? DocumentId { get; set; }
        public float[]? Embedding { get; set; }
        public int Dimensions { get; set; }
        public string? Message { get; set; }
    }
}