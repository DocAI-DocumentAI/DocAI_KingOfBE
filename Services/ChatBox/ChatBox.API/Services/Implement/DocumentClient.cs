using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class DocumentClient : IDocumentClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DocumentClient> _logger;

        public DocumentClient(HttpClient httpClient, ILogger<DocumentClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SearchDocumentResponseExternal> SearchRelevantDocumentsAsync(SearchDocumentRequestExternal request) // REVIEW POINT: Dùng SearchDocumentRequestExternal & SearchDocumentResponseExternal
        {
            _logger.LogInformation($"Calling Document Microservice for document search. Query: {request.Query?.Substring(0, Math.Min(request.Query.Length, 100))}");

            var httpResponse = await _httpClient.PostAsJsonAsync("api/documents/search", request).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            var searchResponse = await httpResponse.Content.ReadFromJsonAsync<SearchDocumentResponseExternal>().ConfigureAwait(false);
            return searchResponse ?? throw new InvalidOperationException("Document Microservice returned an empty or invalid search response.");
        }
    }
}
