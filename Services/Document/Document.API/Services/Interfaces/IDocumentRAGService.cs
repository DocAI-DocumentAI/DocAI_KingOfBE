using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentRAGService
    {
        Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request);

        Task<DocumentRAGResponse> SearchOfficialDocumentsAsync(DocumentRAGRequest request);

        Task<string> GetRAGAnswerAsync(string query, string userId);
        Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId);
    }
}
