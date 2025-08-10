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
        Task<bool> DismissNotificationByUserAsync(Guid logId, Guid userId);
        Task<string> DismissNotificationByTokenAsync(Guid token);

    }
}
