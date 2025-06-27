using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentClient
    {
        Task<SearchDocumentResponseExternal> SearchRelevantDocumentsAsync(SearchDocumentRequestExternal request); // REVIEW POINT: Dùng SearchDocumentRequestExternal & SearchDocumentResponseExternal

    }
}
