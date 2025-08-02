using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentSearchService
    {
        Task<DocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5);
        Task<string> GetRAGAnswerAsync(string query, string userId);
        Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId);

        Task<DocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId);
    }
}
