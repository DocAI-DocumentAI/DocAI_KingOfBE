using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Interface
{
    public interface IProviderFactory
    {
        ITextGenerationService CreateHuggingFaceTextService(string modelId, string apiKey, string endpoint);
        IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbeddingService(string apiKey, string model = "text-embedding-3-small");
    }
}
