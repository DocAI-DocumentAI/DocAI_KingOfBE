using AI.API.Services.Implement;
using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Implement
{
    public class KernelProviderService : IKernelProviderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<KernelProviderService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMemoryCache _cache;

        public KernelProviderService(
       IHttpClientFactory httpClientFactory,
       ILogger<KernelProviderService> logger,
       ILoggerFactory loggerFactory,
       IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _cache = cache;
        }

        public async Task<ITextGenerationService> CreateTextGenerationServiceAsync(AIModelConfiguration config)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
                throw new ArgumentException("API Key is required", nameof(config));

            _logger.LogInformation("Creating text generation service for {ProviderType} model: {ModelId}",
                 config.ProviderType, config.ModelId);

            try
            {
                return config.ProviderType switch
                {
                    AIProviderType.OpenAI => await CreateOpenAIServiceAsync(config),
                    AIProviderType.HuggingFace => await CreateHuggingFaceServiceAsync(config),
                    AIProviderType.MistralAI => await CreateMistralServiceAsync(config),
                    AIProviderType.GoogleGemini => await CreateGeminiServiceAsync(config),
                    AIProviderType.AzureAIInference => await CreateAzureServiceAsync(config),
                    _ => throw new NotSupportedException($"Provider type {config.ProviderType} is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create text generation service for {ProviderType} model: {ModelId}",
                    config.ProviderType, config.ModelId);
                throw;
            }
        }
        private async Task<ITextGenerationService> CreateOpenAIServiceAsync(AIModelConfiguration config)
        {
            // Cache key for reusing HttpClient and service instances
            var cacheKey = $"openai_{config.ModelId}_{config.ApiKey.GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out ITextGenerationService cachedService))
            {
                _logger.LogDebug("Using cached OpenAI service for model: {ModelId}", config.ModelId);
                return cachedService;
            }

            var httpClient = _httpClientFactory.CreateClient("OpenAIClient");
            var kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070

            kernelBuilder.AddOpenAIChatCompletion(
                modelId: config.ModelId,
                apiKey: config.ApiKey,
                httpClient: httpClient
            );
#pragma warning restore SKEXP0070

            var kernel = kernelBuilder.Build();
            var service = kernel.GetRequiredService<ITextGenerationService>();

            // Cache for 30 minutes
            _cache.Set(cacheKey, service, TimeSpan.FromMinutes(30));

            _logger.LogDebug("Created and cached OpenAI service for model: {ModelId}", config.ModelId);
            return service;
        }
        private async Task<ITextGenerationService> CreateHuggingFaceServiceAsync(AIModelConfiguration config)
        {
            // HuggingFace - Direct HTTP approach (fastest)
            var httpClient = _httpClientFactory.CreateClient("HuggingFaceClient");
            var endpoint = config.Endpoint ?? "https://router.huggingface.co/v1/chat/completions";
            var logger = _loggerFactory.CreateLogger<HuggingFaceTextService>();

            _logger.LogDebug("Creating HuggingFace service with model: {ModelId}, endpoint: {Endpoint}",
                config.ModelId, endpoint);

            return new HuggingFaceTextService(
                httpClient: httpClient,
                apiKey: config.ApiKey,
                model: config.ModelId,
                endpoint: endpoint,
                logger: logger
            );
        }
        private async Task<ITextGenerationService> CreateMistralServiceAsync(AIModelConfiguration config)
        {
            var cacheKey = $"mistral_{config.ModelId}_{config.ApiKey.GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out ITextGenerationService cachedService))
            {
                _logger.LogDebug("Using cached MistralAI service for model: {ModelId}", config.ModelId);
                return cachedService;
            }

            var httpClient = _httpClientFactory.CreateClient("MistralClient");
            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddMistralChatCompletion(
                modelId: config.ModelId,
                apiKey: config.ApiKey,
                httpClient: httpClient
            );

            var kernel = kernelBuilder.Build();
            var service = kernel.GetRequiredService<ITextGenerationService>();

            _cache.Set(cacheKey, service, TimeSpan.FromMinutes(30));

            _logger.LogDebug("Created and cached MistralAI service for model: {ModelId}", config.ModelId);
            return service;
        }
        private async Task<ITextGenerationService> CreateGeminiServiceAsync(AIModelConfiguration config)
        {
            var cacheKey = $"gemini_{config.ModelId}_{config.ApiKey.GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out ITextGenerationService cachedService))
            {
                _logger.LogDebug("Using cached GoogleGemini service for model: {ModelId}", config.ModelId);
                return cachedService;
            }

            var httpClient = _httpClientFactory.CreateClient("GeminiClient");
            var kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0070

            kernelBuilder.AddGoogleAIGeminiChatCompletion(
                modelId: config.ModelId,
                apiKey: config.ApiKey,
                httpClient: httpClient
            );
#pragma warning restore SKEXP0070

            var kernel = kernelBuilder.Build();
            var service = kernel.GetRequiredService<ITextGenerationService>();

            _cache.Set(cacheKey, service, TimeSpan.FromMinutes(30));

            _logger.LogDebug("Created and cached GoogleGemini service for model: {ModelId}", config.ModelId);
            return service;
        }

        private async Task<ITextGenerationService> CreateAzureServiceAsync(AIModelConfiguration config)
        {
            if (string.IsNullOrEmpty(config.Endpoint))
                throw new ArgumentException("Endpoint is required for Azure AI Inference");

            var cacheKey = $"azure_{config.ModelId}_{config.Endpoint}_{config.ApiKey.GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out ITextGenerationService cachedService))
            {
                _logger.LogDebug("Using cached Azure AI service for model: {ModelId}", config.ModelId);
                return cachedService;
            }

            var httpClient = _httpClientFactory.CreateClient("AzureClient");
            var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070
            kernelBuilder.AddAzureAIInferenceChatCompletion(
                modelId: config.ModelId,
                apiKey: config.ApiKey,
                endpoint: new Uri(config.Endpoint),
                httpClient: httpClient
            );
#pragma warning restore SKEXP0070

            var kernel = kernelBuilder.Build();
            var service = kernel.GetRequiredService<ITextGenerationService>();

            _cache.Set(cacheKey, service, TimeSpan.FromMinutes(30));

            _logger.LogDebug("Created and cached Azure AI service for model: {ModelId}", config.ModelId);
            return service;
        }
    }
}
