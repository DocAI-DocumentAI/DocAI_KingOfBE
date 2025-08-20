using Notification.API.Payload.Response;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationReadService
    {
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllAsReadAsync(string userEmail);
        Task<int> GetUnreadCountAsync(string userEmail);
        Task<IPaginate<NotificationResponse>> GetUserNotificationsAsync(
            string userEmail, int page, int size, bool? isRead = null);
    }
}
