using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentRAGService
    {
        /// <summary>
        /// ✅ UPDATED: Search documents and return raw content instead of AI-generated answer
        /// </summary>
        Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request);

        /// <summary>
        /// ✅ NEW: Get raw content only for simple queries
        /// </summary>
        Task<string> GetRawContentAsync(string query, string userId);

        /// <summary>
        /// ✅ NEW: Get raw content with sources
        /// </summary>
        Task<(string RawContent, List<DocumentSourceResponse> Sources)> GetRawContentWithSourcesAsync(string query, string userId);

    }
}
