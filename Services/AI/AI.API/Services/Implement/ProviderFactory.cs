using AI.API.Services.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Implement
{
    public class ProviderFactory : IProviderFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        public ProviderFactory(
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
        }

        public ITextGenerationService CreateHuggingFaceTextService(string modelId, string apiKey, string endpoint)
        {
            var httpClient = _httpClientFactory.CreateClient("HuggingFaceClient");
            var logger = _loggerFactory.CreateLogger<HuggingFaceTextService>();

            return new HuggingFaceTextService(
                httpClient: httpClient,
                apiKey: apiKey,
                model: modelId,
                endpoint: endpoint,
                logger: logger);
        }

        public IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbeddingService(string apiKey, string model = "text-embedding-3-small")
        {
            // Create OpenAI embedding service with fixed model
            // This is for the fixed OpenAI embedding service, not dynamic
            throw new NotImplementedException("OpenAI embedding service should be configured statically in DI");
        }
    }

public static class LoggerFactoryExtensions
    {
        public static ILogger<T> CreateLogger<T>(this ILogger logger)
        {
            if (logger is ILogger<T> typedLogger)
                return typedLogger;

            // Fallback - you might need to inject ILoggerFactory instead
            throw new NotSupportedException("Cannot create typed logger from generic logger");
        }
    }
}