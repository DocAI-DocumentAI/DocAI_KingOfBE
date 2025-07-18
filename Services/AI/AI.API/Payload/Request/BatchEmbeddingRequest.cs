namespace AI.API.Payload.Request
{
    public class BatchEmbeddingRequest
    {
        public List<EmbeddingRequest> Documents { get; set; } = new();

    }
}
