using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response.DocumentServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;

namespace ChatBox.API.Services.Interfaces
{
    public interface IDocumentServiceClient
    {
        // Core Search
        Task<DocumentSearchResponse> SearchDocumentsAsync(DocumentSearchRequest request);
        Task<List<DocumentCitation>> SearchDocumentsByIdsAsync(List<string> documentIds, Guid userId);

        // Access Control
        Task<DocumentAccessResponse> CheckDocumentAccessAsync(DocumentAccessRequest request);
        Task<BatchDocumentResponse> CheckBatchAccessAsync(BatchDocumentRequest request);

        // Document Management
        Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId, Guid userId);
        Task<BatchDocumentResponse> GetBatchMetadataAsync(BatchDocumentRequest request);
        Task<DocumentContent> GetDocumentContentAsync(string documentId, Guid userId);

        // Status & Health
        Task<DocumentStatusResponse> CheckDocumentStatusAsync(DocumentStatusRequest request);
        Task<List<string>> GetDocumentCategoriesAsync();

        // Analytics & Recommendations
        //Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, Guid userId);
    }
}
