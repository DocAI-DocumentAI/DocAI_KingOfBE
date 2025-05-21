using AI.API.Payload.Response;
using AI.API.Services.Interface;

namespace AI.API.Services.Implement
{
    public class OpenAIChatCompletionService : IChatCompletionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OpenAIChatCompletionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            // Setup HttpClient
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration["OpenAI:ApiKey"]}");
        }

        public async Task<string> GetCompletionAsync(string sessionId, List<(string Role, string Content)> messages, Dictionary<string, object> settings = null)
        {
            // Transform messages to OpenAI format
            var chatMessages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList();

            var defaultSettings = new Dictionary<string, object>
            {
                ["model"] = _configuration["OpenAI:ModelName"] ?? "gpt-4o",
                ["temperature"] = 0.7,
                ["max_tokens"] = 1000
            };

            // Override with custom settings if provided
            if (settings != null)
            {
                foreach (var setting in settings)
                {
                    defaultSettings[setting.Key] = setting.Value;
                }
            }

            // Build request
            var requestBody = new Dictionary<string, object>(defaultSettings)
            {
                ["messages"] = chatMessages
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>();
            return result.Choices[0].Message.Content;
        }

     
    }
}
