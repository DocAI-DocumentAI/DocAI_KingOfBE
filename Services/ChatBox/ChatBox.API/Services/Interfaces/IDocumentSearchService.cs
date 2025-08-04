using ChatBox.API.Payload.Response;
using Shared.DTOs;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentSearchService
    {
        Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5);
        Task<ChatBoxDocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId);
        Task<string> GetRAGAnswerAsync(string query, string userId);
        Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId);
    }
}
