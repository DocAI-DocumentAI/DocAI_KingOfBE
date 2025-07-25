using AI.API.Services.Implement;
using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Implement
{
    public class KernelProviderService : IKernelProviderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<KernelProviderService> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public KernelProviderService(
            IHttpClientFactory httpClientFactory,
            ILogger<KernelProviderService> logger,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        public async Task<ITextGenerationService> CreateTextGenerationServiceAsync(AIModelConfiguration config)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
                throw new ArgumentException("API Key is required", nameof(config));

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            var kernelBuilder = Kernel.CreateBuilder();

            try
            {
                switch (config.ProviderType)
                {
                    case AIProviderType.OpenAI:
                        kernelBuilder.AddOpenAIChatCompletion(
                            modelId: config.ModelId,
                            apiKey: config.ApiKey,
                            httpClient: httpClient
                        );
                        break;

                    case AIProviderType.MistralAI:
                        kernelBuilder.AddMistralChatCompletion(
                            modelId: config.ModelId,
                            apiKey: config.ApiKey,
                            httpClient: httpClient
                        );
                        break;

                    case AIProviderType.GoogleGemini:
                        kernelBuilder.AddGoogleAIGeminiChatCompletion(
                            modelId: config.ModelId,
                            apiKey: config.ApiKey,
                            httpClient: httpClient
                        );
                        break;

                    case AIProviderType.AzureAIInference:
                        if (string.IsNullOrEmpty(config.Endpoint))
                            throw new ArgumentException("Endpoint is required for Azure AI Inference");
#pragma warning disable SKEXP0070
                        kernelBuilder.AddAzureAIInferenceChatCompletion(
                            modelId: config.ModelId,
                            apiKey: config.ApiKey,
                            endpoint: new Uri(config.Endpoint),
                            httpClient: httpClient
                        );
#pragma warning restore SKEXP0070
                        break;

                    case AIProviderType.HuggingFace:
                        var endpoint = config.Endpoint ?? "https://router.huggingface.co/v1/chat/completions";
                        return new HuggingFaceTextService(
                            httpClient: httpClient,
                            apiKey: config.ApiKey,
                            model: config.ModelId,
                            endpoint: endpoint,
                            logger: _loggerFactory.CreateLogger<HuggingFaceTextService>()
                        );

                    default:
                        throw new ArgumentException($"Unsupported provider type: {config.ProviderType}");
                }

                var kernel = kernelBuilder.Build();
                return kernel.GetRequiredService<ITextGenerationService>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create text generation service for {Provider} model {ModelId}", 
                    config.ProviderType, config.ModelId);
                throw;
            }
        }
    }
}
