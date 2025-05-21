namespace AI.API.Services.Interface
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingsAsync(string text, string modelName = "default");
        Task<List<(float[] Embedding, float Score)>> FindSimilarEmbeddingsAsync(float[] embedding, int limit = 5, float minScore = 0.7f);
    }
}
