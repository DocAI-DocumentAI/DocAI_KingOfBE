using Shared.DTOs;

namespace Notification.API.Services.Interfaces
{
    public interface IUserPreferencesService
    {
        Task<UserNotificationPreferencesDto?> GetUserPreferencesAsync(Guid userId);
        Task<List<UserDto>> FilterUsersByPreferencesAsync(List<UserDto> users, string notificationType);
        Task<bool> ShouldSendEmailNotificationAsync(Guid userId, string notificationType);
        Task<bool> ShouldSendSystemNotificationAsync(Guid userId, string notificationType);
    }
}
