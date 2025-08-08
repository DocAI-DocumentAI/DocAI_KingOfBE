using Shared.Models;

namespace Document.API.Services.Interfaces
{
    public interface IDocumentExpirationService
    {
        Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate);
        Task<bool> UpdateDocumentStatusAsync(Guid documentId, string version, string newStatus);
        Task<bool> DeactivateDocumentWarningsAsync(Guid documentId, string version);
    }
}
