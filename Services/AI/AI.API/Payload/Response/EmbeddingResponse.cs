namespace AI.API.Payload.Response
{
    public class EmbeddingResponse
    {
        public float[] Embedding { get; set; }
        public int Dimensions { get; set; }
        public string ModelName { get; set; }
    }
}
