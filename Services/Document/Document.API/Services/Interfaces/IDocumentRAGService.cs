using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentRAGService
    {
        Task<DocumentRAGResponse> SearchDocumentsWithRAGAsync(DocumentRAGRequest request);

    }
}
