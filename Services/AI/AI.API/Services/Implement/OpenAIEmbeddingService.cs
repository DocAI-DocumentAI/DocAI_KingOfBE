using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AI.API.Services.Implement
{
    public class OpenAIEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly DocAIDbContext _dbContext;

        public OpenAIEmbeddingService(HttpClient httpClient, IConfiguration configuration, DocAIDbContext dbContext)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _dbContext = dbContext;

            // Setup HttpClient
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration["OpenAI:ApiKey"]}");
        }

        public async Task<float[]> GetEmbeddingsAsync(string text, string modelName = "default")
        {
            var model = await _dbContext.EmbeddingModels
                .FirstOrDefaultAsync(m => m.IsActive && (modelName == "default" || m.Name == modelName))
                ?? throw new InvalidOperationException("No active embedding model found");

            var requestBody = new
            {
                input = text,
                model = "text-embedding-3-large" // Use OpenAI model, configured based on EmbeddingModel info
            };

            var response = await _httpClient.PostAsJsonAsync("embeddings", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
            return result.Data[0].Embedding;
        }

        public async Task<List<(float[] Embedding, float Score)>> FindSimilarEmbeddingsAsync(float[] embedding, int limit = 5, float minScore = 0.7f)
        {
            // Execute PostgreSQL vector similarity search
            // This uses raw SQL until EF Core gets better PGVector support
            var results = await _dbContext.ChatMessages
                .FromSqlRaw(@"
                    SELECT * FROM ""ChatMessages"" 
                    WHERE ""Embedding"" <#> @embedding < @distance_threshold
                    ORDER BY ""Embedding"" <#> @embedding
                    LIMIT @limit",
                    new NpgsqlParameter("@embedding", embedding),
                    new NpgsqlParameter("@distance_threshold", 1 - minScore), // Convert similarity threshold to distance
                    new NpgsqlParameter("@limit", limit))
                .ToListAsync();

            return results.Select(r => (r.Embedding, Score: CosineSimilarity(embedding, r.Embedding))).ToList();
        }
        private float CosineSimilarity(float[] a, float[] b)
        {
            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}
