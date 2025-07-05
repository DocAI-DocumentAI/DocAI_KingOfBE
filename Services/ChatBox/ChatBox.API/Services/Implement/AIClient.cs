using System.Text.Json;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ChatBox.API.Services.Implement
{
    public class AIClient : IAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIClient> _logger;
        private readonly string _baseAddress; 
        private readonly IConfiguration _configuration;
        public AIClient(
        ILogger<AIClient> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _baseAddress = _configuration["ChatService:AIMicroserviceBaseUrl"]
            ?? throw new InvalidOperationException("AI Microservice Base URL is missing.");

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(_baseAddress);
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Giảm timeout xuống 60s để tối ưu

        _logger.LogInformation("AIClient initialized with BaseAddress: {BaseAddress}", _baseAddress);
    }

        public async Task<AIResponseExternal> GenerateAIResponseAsync(AIRequestExternal request)
        {
            if (request == null || string.IsNullOrEmpty(request.Question))
            {
                _logger.LogError("GenerateAIResponseAsync received null or empty request/question.");
                throw new ArgumentException("Request or question cannot be null or empty.");
            }

            _logger.LogInformation("Calling AI Microservice for non-streaming response. Question: {Question}",
                request.Question.Length > 100 ? request.Question.Substring(0, 100) + "..." : request.Question);

            request.StreamResponse = false;

            try
            {
                var httpResponse = await _httpClient.PostAsJsonAsync("api/ai/generate", request)
                    .ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();

                var apiResponse = await httpResponse.Content.ReadFromJsonAsync<AIResponseExternal>()
                    .ConfigureAwait(false);
                return apiResponse ?? throw new InvalidOperationException("AI Microservice returned an empty response.");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP Error calling AI Generate API: {Message}. Status: {StatusCode}. BaseAddress: {BaseAddress}",
                    httpEx.Message, httpEx.StatusCode, _baseAddress);
                throw new ApplicationException($"Failed to communicate with AI Microservice: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AIClient during GenerateAIResponseAsync. BaseAddress: {BaseAddress}", _baseAddress);
                throw new ApplicationException($"An unexpected error occurred during AI generation: {ex.Message}", ex);
            }
        }

        public async IAsyncEnumerable<string> StreamAIResponseAsync(AIRequestExternal request)
        {
            if (request == null || string.IsNullOrEmpty(request.Question))
            {
                _logger.LogError("StreamAIResponseAsync received null or empty request/question.");
                throw new ArgumentException("Request or question cannot be null or empty.");
            }

            _logger.LogInformation("Calling AI Microservice for streaming response. Question: {Question}",
                request.Question.Length > 100 ? request.Question.Substring(0, 100) + "..." : request.Question);

            request.StreamResponse = true;

            Stream responseStream;
            try
            {
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "api/ai/generate")
                {
                    Content = JsonContent.Create(request)
                };

                var httpResponse = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                httpResponse.EnsureSuccessStatusCode();

                responseStream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP Error calling AI Stream API: {Message}. Status: {StatusCode}. BaseAddress: {BaseAddress}",
                    httpEx.Message, httpEx.StatusCode, _baseAddress);
                throw new ApplicationException($"Failed to stream from AI Microservice: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AIClient during StreamAIResponseAsync. BaseAddress: {BaseAddress}", _baseAddress);
                throw new ApplicationException($"An unexpected error occurred during AI streaming: {ex.Message}", ex);
            }

            await using (responseStream)
            using (var reader = new StreamReader(responseStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line != null)
                    {
                        yield return line;
                    }
                }
            }
        }
    }
}
