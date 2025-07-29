using System.Text.Json;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public DocumentSearchService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<List<string>> SearchDocumentsAsync(string query, int limit = 5)
        {
            var documentApiUrl = _configuration["DocumentService:BaseUrl"];
            var response = await _httpClient.GetAsync($"{documentApiUrl}/api/documents/search?query={Uri.EscapeDataString(query)}&limit={limit}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var documents = JsonSerializer.Deserialize<List<string>>(content);
                return documents ?? new List<string>();
            }

            return new List<string>();
        }

        public async Task<string> GetDocumentContentAsync(string documentId)
        {
            var documentApiUrl = _configuration["DocumentService:BaseUrl"];
            var response = await _httpClient.GetAsync($"{documentApiUrl}/api/documents/{documentId}/content");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return string.Empty;
        }
    }
}
