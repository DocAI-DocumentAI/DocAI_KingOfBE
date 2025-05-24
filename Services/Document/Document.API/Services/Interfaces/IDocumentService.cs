using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces;

public interface IDocumentService
{
    Task UploadDocumentAsync(UploadDocumentRequest request);
    Task<DocumentResponse> GetDocumentByIdAsync(string documentId);
    Task<DocumentFileResponse> UpdateMetaDataDocumentAsync(string documentId, UpdateMetaDataReqest request);
    Task DeleteDocumentAsync(string documentId);
    //Task<IEnumerable<Document>> GetAllDocumentsAsync();
}
