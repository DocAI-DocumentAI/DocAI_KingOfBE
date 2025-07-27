using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response.DocumentServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentServiceClient
    {
        // Core Search
        Task<DocumentSearchResponse> SearchDocumentsAsync(DocumentSearchRequest request);
        // Access Control
        Task<DocumentAccessResponse> CheckDocumentAccessAsync(DocumentAccessRequest request);
        Task<BatchDocumentResponse> CheckBatchAccessAsync(BatchDocumentRequest request);

        // Document Management
        Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId, Guid userId);
        Task<DocumentContent> GetDocumentContentAsync(string documentId, Guid userId);

        // Status & Health
        Task<DocumentStatusResponse> CheckDocumentStatusAsync(DocumentStatusRequest request);
        Task<List<string>> GetDocumentCategoriesAsync();

        // Analytics & Recommendations
        //Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, Guid userId);
    }
}
