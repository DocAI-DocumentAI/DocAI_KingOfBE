using Auth.API.Payload.Request.Department;
using Auth.API.Payload.Response.Department;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Auth.API.Services.Implement
{
    public class UserSettingService : IUserSettingService
    {
        private readonly IUnitOfWork<DocAIAuthContext> _unitOfWork;
        private readonly ILogger<UserSettingService> _logger;
        private readonly IMemoryCache _cache;

        public UserSettingService(
            IUnitOfWork<DocAIAuthContext> unitOfWork,
            ILogger<UserSettingService> logger,
            IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cache = cache;
        }

        public async Task<UserSetting?> GetUserSettingByUserIdAsync(Guid userId)
        {
            var cacheKey = $"user_setting_{userId}";
            if (_cache.TryGetValue(cacheKey, out UserSetting? cached) && cached != null)
                return cached;

            try
            {
                var setting = await _unitOfWork.GetRepository<UserSetting>()
                    .SingleOrDefaultAsync(predicate: s => s.UserId == userId);

                if (setting != null)
                {
                    _cache.Set(cacheKey, setting, TimeSpan.FromMinutes(10));
                }

                return setting;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user setting for {UserId}", userId);
                return null;
            }
        }

        public async Task<UserSetting> CreateOrUpdateUserSettingAsync(Guid userId, UserSetting setting)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<UserSetting>();
                var existingSetting = await repo.SingleOrDefaultAsync(predicate: s => s.UserId == userId);

                if (existingSetting != null)
                {
                    // Update existing
                    existingSetting.NotificationsEnabled = setting.NotificationsEnabled;
                    existingSetting.TwoFactorEnabled = setting.TwoFactorEnabled;
                    existingSetting.TwoFactorMethod = setting.TwoFactorMethod;
                    existingSetting.UpdateAt = DateTime.UtcNow;

                    repo.UpdateAsync(existingSetting);
                    await _unitOfWork.CommitAsync();

                    _cache.Remove($"user_setting_{userId}");
                    return existingSetting;
                }
                else
                {
                    // Create new
                    setting.Id = Guid.NewGuid();
                    setting.UserId = userId;
                    setting.UpdateAt = DateTime.UtcNow;

                    await repo.InsertAsync(setting);
                    await _unitOfWork.CommitAsync();

                    return setting;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating user setting for {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdateNotificationPreferencesAsync(Guid userId, bool notificationsEnabled)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<UserSetting>();
                var setting = await repo.SingleOrDefaultAsync(predicate: s => s.UserId == userId);

                if (setting != null)
                {
                    setting.NotificationsEnabled = notificationsEnabled;
                    setting.UpdateAt = DateTime.UtcNow;
                    repo.UpdateAsync(setting);
                }
                else
                {
                    setting = new UserSetting
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        NotificationsEnabled = notificationsEnabled,
                        TwoFactorEnabled = false,
                        UpdateAt = DateTime.UtcNow
                    };
                    await repo.InsertAsync(setting);
                }

                await _unitOfWork.CommitAsync();
                _cache.Remove($"user_setting_{userId}");

                _logger.LogInformation("Updated notification preferences for user {UserId}: {Enabled}",
                    userId, notificationsEnabled);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification preferences for {UserId}", userId);
                return false;
            }
        }
    }
}
