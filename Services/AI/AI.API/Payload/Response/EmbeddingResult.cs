namespace AI.API.Payload.Response
{
    public class EmbeddingResult
    {
        public string DocumentId { get; set; }
        public bool Success { get; set; }
        public float[]? Embedding { get; set; }
        public int Dimensions { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
