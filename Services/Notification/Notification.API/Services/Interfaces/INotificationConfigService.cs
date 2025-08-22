using Notification.API.Payload.Request;
using Notification.API.Payload.Response;

namespace Notification.API.Services.Interfaces
{
    public interface INotificationConfigService
    {
        Task<NotificationConfigResponse> GetNotificationConfigAsync();
        Task<NotificationConfigResponse> UpdateNotificationConfigAsync(NotificationConfigRequest request);
        Task<DateTime?> GetNextRunTimeAsync(string cronExpression);
        Task<object> GetConfigWithStatusAsync();
    }
}
