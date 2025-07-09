using Notification.API.Payload.Response;

namespace Notification.API.Services.Interfaces
{
    public interface IDocumentScanService
    {
        Task ScanAndProcessDocumentsAsync();

    }
}
