namespace AI.API.Payload.Response
{
    public class OpenAIEmbeddingResponse
    {
        public List<EmbeddingData> Data { get; set; }

        public class EmbeddingData
        {
            public float[] Embedding { get; set; }
        }
    }
}
