using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class AIClient : IAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIClient> _logger;

        public AIClient(HttpClient httpClient, ILogger<AIClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIResponseExternal> GenerateAIResponseAsync(AIRequestExternal request) // REVIEW POINT: Dùng AIRequestExternal & AIResponseExternal
        {
            _logger.LogInformation($"Calling AI Microservice for non-streaming response. Question: {request.Question?.Substring(0, Math.Min(request.Question.Length, 100))}");

            request.StreamResponse = false;

            var httpResponse = await _httpClient.PostAsJsonAsync("api/ai/generate", request).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            var apiResponse = await httpResponse.Content.ReadFromJsonAsync<AIResponseExternal>().ConfigureAwait(false);
            return apiResponse ?? throw new InvalidOperationException("AI Microservice returned an empty or invalid response.");
        }

        public async IAsyncEnumerable<string> StreamAIResponseAsync(AIRequestExternal request) // REVIEW POINT: Dùng AIRequestExternal
        {
            _logger.LogInformation($"Calling AI Microservice for streaming response. Question: {request.Question?.Substring(0, Math.Min(request.Question.Length, 100))}");

            request.StreamResponse = true;

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "api/ai/generate")
            {
                Content = JsonContent.Create(request)
            };

            var httpResponse = await _httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream);

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
