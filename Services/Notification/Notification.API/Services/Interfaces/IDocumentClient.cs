using Notification.API.Payload.Response;

namespace Notification.API.Services.Interfaces
{
    public interface IDocumentClient
    {
        Task<List<DocumentDetailResponseExternal>> GetDocumentsForExpirationCheckAsync(DateTime warningDate);

        Task<bool> UpdateDocumentStatusAsync(Guid documentId, string version, string newStatus);

        Task<bool> DeactivateDocumentWarningsAsync(Guid documentId, string version);

    }
}
