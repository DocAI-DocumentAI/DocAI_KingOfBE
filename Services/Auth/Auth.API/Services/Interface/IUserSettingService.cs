using Auth.Domain.Models;

namespace Auth.API.Services.Interface
{
    public interface IUserSettingService
    {
        Task<UserSetting?> GetUserSettingByUserIdAsync(Guid userId);
        Task<UserSetting> CreateOrUpdateUserSettingAsync(Guid userId, UserSetting setting);
        Task<bool> UpdateNotificationPreferencesAsync(Guid userId, bool notificationsEnabled);

    }
}
