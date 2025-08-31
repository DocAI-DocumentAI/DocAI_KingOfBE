using Shared.Models;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentExpirationService
    {
        Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate);
        Task<bool> UpdateDocumentStatusAsync(string documentId, string version, string newStatus);
        Task<bool> DeactivateDocumentWarningsAsync(string documentId, string version);
        Task<List<DocumentExpirationDto>> GetDocumentsRequiringStatusUpdateAsync();

    }
}
