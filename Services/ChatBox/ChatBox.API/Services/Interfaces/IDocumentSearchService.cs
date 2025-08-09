using ChatBox.API.Payload.Response;
using Shared.DTOs;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentSearchService
    {

        Task<string> GetRawContentAsync(string query, string userId);
        Task<(string RawContent, List<DocumentInfo> Sources)> GetRawContentWithSourcesAsync(string query, string userId);

        Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 3);
    }
}
