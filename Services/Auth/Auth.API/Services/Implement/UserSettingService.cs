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

        /// <summary>
        /// NEW METHOD - For notification system integration with Guid IDs
        /// </summary>
        public async Task<Dictionary<Guid, string>> GetDepartmentNamesByIdsAsync(List<Guid> departmentIds)
        {
            if (!departmentIds.Any())
                return new Dictionary<Guid, string>();

            try
            {
                // Check cache first
                var cachedResults = new Dictionary<Guid, string>();
                var uncachedIds = new List<Guid>();

                foreach (var id in departmentIds)
                {
                    var cacheKey = $"dept_name_{id}";
                    if (_cache.TryGetValue(cacheKey, out string? cachedName) && !string.IsNullOrEmpty(cachedName))
                    {
                        cachedResults[id] = cachedName;
                        _logger.LogDebug("Cache hit for department {DepartmentId}", id);
                    }
                    else
                    {
                        uncachedIds.Add(id);
                    }
                }

                // Query database for uncached IDs
                if (uncachedIds.Any())
                {
                    _logger.LogInformation("Querying database for {Count} uncached department names", uncachedIds.Count);

                    var departments = await _unitOfWork.GetRepository<Department>()
                        .GetListAsync(
                            predicate: d => uncachedIds.Contains(d.Id),
                            selector: d => new { d.Id, d.Name }
                        );

                    foreach (var dept in departments)
                    {
                        var departmentName = string.IsNullOrEmpty(dept.Name) ? "Unknown Department" : dept.Name;
                        cachedResults[dept.Id] = departmentName;

                        // Cache for 30 minutes
                        var cacheKey = $"dept_name_{dept.Id}";
                        _cache.Set(cacheKey, departmentName, TimeSpan.FromMinutes(30));

                        _logger.LogDebug("Cached department name: {DepartmentId} -> {Name}", dept.Id, departmentName);
                    }

                    // Add fallback names for missing departments
                    var foundIds = departments.Select(d => d.Id).ToHashSet();
                    foreach (var missingId in uncachedIds.Where(id => !foundIds.Contains(id)))
                    {
                        var fallbackName = $"Department-{missingId.ToString()[..8]}";
                        cachedResults[missingId] = fallbackName;

                        // Cache fallback for shorter time (5 minutes) in case department gets created
                        var cacheKey = $"dept_name_{missingId}";
                        _cache.Set(cacheKey, fallbackName, TimeSpan.FromMinutes(5));

                        _logger.LogWarning("Department {DepartmentId} not found, using fallback name: {FallbackName}",
                            missingId, fallbackName);
                    }
                }

                _logger.LogInformation("Retrieved names for {Found}/{Total} departments (Cache: {Cached}, DB: {Queried})",
                    cachedResults.Count, departmentIds.Count,
                    departmentIds.Count - uncachedIds.Count, uncachedIds.Count);

                return cachedResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department names for {Count} departments", departmentIds.Count);

                // Return fallback names for all departments on error
                return departmentIds.ToDictionary(
                    id => id,
                    id => $"Department-{id.ToString()[..8]}"
                );
            }
        }

        ///// <summary>
        ///// Helper method to clear department name cache when department is updated
        ///// </summary>
        //private void ClearDepartmentNameCache(Guid departmentId)
        //{
        //    var cacheKey = $"dept_name_{departmentId}";
        //    _cache.Remove(cacheKey);
        //    _logger.LogDebug("Cleared cache for department {DepartmentId}", departmentId);
        //}

        ///// <summary>
        ///// Override update method to clear cache
        ///// </summary>
        //public async Task<DepartmentResponse> UpdateDepartmentWithCacheClearAsync(UpdateDepartmentRequest request, Guid departmentId)
        //{
        //    var result = await UpdateDepartmentAsync(request, departmentId);

        //    // Clear cache after successful update
        //    if (result != null)
        //    {
        //        ClearDepartmentNameCache(departmentId);
        //    }

        //    return result;
        //}
    }
}
