using AI.API.Services.Interface;
using AI.Domain.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Implement
{
    public class DynamicProviderService : IDynamicProviderService
    {
        private readonly IProviderFactory _providerFactory;
        private readonly IAIConfigurationService _configService;
        private readonly ILogger<DynamicProviderService> _logger;
        private readonly HttpClient _httpClient;

        public DynamicProviderService(
            IProviderFactory providerFactory,
            IAIConfigurationService configService,
            ILogger<DynamicProviderService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _providerFactory = providerFactory;
            _configService = configService;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("HuggingFaceClient");
        }

        public async Task<ITextGenerationService> CreateTextGenerationServiceAsync(string modelId, string apiKey)
        {
            try
            {
                _logger.LogInformation("Creating dynamic text generation service for model: {ModelId}", modelId);

                // Fixed endpoint for HuggingFace Router
                const string endpoint = "https://router.huggingface.co/v1/chat/completions";

                // Validate connection first
                var isValid = await ValidateModelConnectionAsync(modelId, apiKey, endpoint);
                if (!isValid)
                {
                    throw new InvalidOperationException($"Cannot connect to model {modelId} with provided configuration");
                }

                // Create service using factory
                var service = _providerFactory.CreateHuggingFaceTextService(modelId, apiKey, endpoint);

                _logger.LogInformation("Successfully created text generation service for model: {ModelId}", modelId);
                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create text generation service for model: {ModelId}", modelId);
                throw;
            }
        }

        public async Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingServiceAsync(ModelType modelType)
        {
            try
            {
                // For embedding, we use fixed OpenAI text-embedding-3-small (không cấu hình động)
                _logger.LogInformation("Retrieved fixed OpenAI embedding service for model type: {ModelType}", modelType);

                // Return the existing OpenAI embedding service from DI container
                return await GetOpenAIEmbeddingServiceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get embedding service for model type: {ModelType}", modelType);
                throw;
            }
        }

        public async Task<bool> ValidateModelConnectionAsync(string modelId, string apiKey, string endpoint)
        {
            try
            {
                _logger.LogDebug("Validating connection for model: {ModelId} at endpoint: {Endpoint}", modelId, endpoint);

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("User-Agent", "DocAI-Validation/1.0");

                // Simple test payload
                var testPayload = new
                {
                    model = modelId,
                    messages = new[] { new { role = "user", content = "test" } },
                    max_tokens = 1,
                    stream = false
                };

                var json = System.Text.Json.JsonSerializer.Serialize(testPayload);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Consider 200, 400 (bad request due to test payload), and 401 (auth issues) as "reachable"
                var isValid = response.IsSuccessStatusCode ||
                             response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                             response.StatusCode == System.Net.HttpStatusCode.Unauthorized;

                _logger.LogInformation("Model validation for {ModelId}: {IsValid} (Status: {StatusCode})",
                    modelId, isValid, response.StatusCode);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Model validation failed for {ModelId}", modelId);
                return false;
            }
        }

        private async Task<IEmbeddingGenerator<string, Embedding<float>>> GetOpenAIEmbeddingServiceAsync()
        {
            // Return the fixed OpenAI embedding service from DI container
            // This should be configured in DependencyService.cs with text-embedding-3-small
            var serviceProvider = _configService as IServiceProvider ??
                throw new InvalidOperationException("Cannot resolve OpenAI embedding service");

            return serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
    }
}
