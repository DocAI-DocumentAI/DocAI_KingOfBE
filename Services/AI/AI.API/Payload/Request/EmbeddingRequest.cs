namespace AI.API.Payload.Request
{
    public class EmbeddingRequest
    {
        public string Text { get; set; }
        public string ModelName { get; set; } = "default";
    }
}
