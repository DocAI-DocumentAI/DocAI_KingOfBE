using Notification.API.Payload.Response;
using Shared.Models;

namespace Notification.API.Services.Interfaces
{
    public interface IDocumentScanService
    {
        Task<List<DocumentExpirationDto>> GetExpiredDocumentsAsync();
        Task<List<DocumentExpirationDto>> GetNearExpiredDocumentsAsync();
        Task ProcessNearExpiredDocumentsAsync(List<DocumentExpirationDto> documents, string jobId);
        Task ProcessExpiredDocumentsAsync(List<DocumentExpirationDto> documents, string jobId);
        Task ProcessExpiredDocumentsDirectlyAsync(List<DocumentExpirationDto> expiredDocs, string scanId);
        Task<List<DocumentExpirationDto>> GetDocumentsForStatusUpdateAsync();
        Task ProcessDocumentStatusUpdatesAsync(List<DocumentExpirationDto> documents, string jobId);
        Task ScanAndProcessDocumentsAsync();
    }
}
