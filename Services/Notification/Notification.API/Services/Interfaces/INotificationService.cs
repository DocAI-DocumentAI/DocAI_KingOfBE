using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Domain.Models;
using Shared.Command;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationService
    {


        Task ProcessNearingExpirationNotification(DocumentDetailResponseExternal document);

        Task ProcessExpiredDocumentNotification(DocumentDetailResponseExternal document);

        Task SendAdminEscalationNotification(Guid? documentId, string? documentVersion, string subject, string errorMessage);

        Task<bool> DismissNotificationByUserAsync(Guid logId, Guid userId);
        Task<string> DismissNotificationByTokenAsync(Guid token);

        Task SendGeneralNotificationAsync(SendGeneralNotificationCommand command);

    }
}
