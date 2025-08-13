using ChatBox.API.Payload.Response;
using Shared.DTOs;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentSearchService
    {

        Task<string> GetRawContentAsync(string query, string userId, string? documentId = null); // ✅ THÊM documentId
        Task<(string RawContent, List<DocumentInfo> Sources)> GetRawContentWithSourcesAsync(string query, string userId, string? documentId = null); // ✅ THÊM documentId

        Task<ChatBoxDocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5, string? documentId = null); // ✅ THÊM documentId
    }
}
