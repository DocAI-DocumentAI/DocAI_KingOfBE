using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Domain.Models;
using Shared.Command;
using Shared.Models;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationService
    {
        Task ProcessExpiredDocumentNotification(DocumentExpirationDto document);
        Task ProcessNearingExpirationNotification(DocumentExpirationDto document);
        Task ProcessWeeklyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName);
        Task ProcessDailyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName);
        Task SendGeneralNotificationAsync(string templateName, string recipientEmail, string recipientName);

    }
}
