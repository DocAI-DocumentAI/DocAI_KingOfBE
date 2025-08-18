using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Domain.Models;
using Shared.Command;
using Shared.Models;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationService
    {
        Task ProcessNearingExpirationNotification(DocumentExpirationDto document);
        Task ProcessExpiredDocumentNotification(DocumentExpirationDto document);
        Task SendGeneralNotificationAsync(string templateName, string recipientEmail, string recipientName);
        Task ProcessWeeklyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName);

    }
}
