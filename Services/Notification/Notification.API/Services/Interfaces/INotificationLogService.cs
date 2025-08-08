using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Domain.Models;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationLogService
    {
        Task CreateLogAsync(NotificationLog log);
        Task<IPaginate<NotificationResponse>> GetNotificationLogsAsync(NotificationRequest request);
        Task CleanUpOldLogsAsync();
    }
}
