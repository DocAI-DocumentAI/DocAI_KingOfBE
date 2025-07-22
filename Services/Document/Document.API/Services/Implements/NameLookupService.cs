using Document.API.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Shared.DTOs;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for looking up user and department names with caching and RabbitMQ
/// </summary>
public class NameLookupService : INameLookupService
{
    private readonly IRequestClient<NameLookupRequest> _requestClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NameLookupService> _logger;
    
    private const int CacheExpirationMinutes = 30;
    private const string UserCachePrefix = "user_name_";
    private const string DepartmentCachePrefix = "dept_name_";

    public NameLookupService(
        IRequestClient<NameLookupRequest> requestClient,
        IMemoryCache cache,
        ILogger<NameLookupService> logger)
    {
        _requestClient = requestClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<NameLookupResponse> GetNamesAsync(List<string> userIds, List<string> departmentIds)
    {
        try
        {
            // Filter out IDs that are already cached
            var uncachedUserIds = userIds.Where(id => !string.IsNullOrEmpty(id) && !_cache.TryGetValue($"{UserCachePrefix}{id}", out _)).ToList();
            var uncachedDepartmentIds = departmentIds.Where(id => !string.IsNullOrEmpty(id) && !_cache.TryGetValue($"{DepartmentCachePrefix}{id}", out _)).ToList();

            var response = new NameLookupResponse();

            // Get cached names
            foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                if (_cache.TryGetValue($"{UserCachePrefix}{userId}", out string? cachedUserName) && cachedUserName != null)
                {
                    response.UserNames[userId] = cachedUserName;
                }
            }

            foreach (var deptId in departmentIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                if (_cache.TryGetValue($"{DepartmentCachePrefix}{deptId}", out string? cachedDeptName) && cachedDeptName != null)
                {
                    response.DepartmentNames[deptId] = cachedDeptName;
                }
            }

            // If all names are cached, return immediately
            if (!uncachedUserIds.Any() && !uncachedDepartmentIds.Any())
            {
                return response;
            }

            // Request uncached names from Auth service via RabbitMQ
            var request = new NameLookupRequest
            {
                UserIds = uncachedUserIds,
                DepartmentIds = uncachedDepartmentIds
            };

            _logger.LogInformation("Requesting names for {UserCount} users and {DeptCount} departments", 
                uncachedUserIds.Count, uncachedDepartmentIds.Count);

            var authResponse = await _requestClient.GetResponse<NameLookupResponse>(request, timeout: TimeSpan.FromSeconds(2));
            var authResult = authResponse.Message;

            if (authResult.Success)
            {
                // Cache and merge the results
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(15)
                };

                foreach (var userKvp in authResult.UserNames)
                {
                    _cache.Set($"{UserCachePrefix}{userKvp.Key}", userKvp.Value, cacheOptions);
                    response.UserNames[userKvp.Key] = userKvp.Value;
                }

                foreach (var deptKvp in authResult.DepartmentNames)
                {
                    _cache.Set($"{DepartmentCachePrefix}{deptKvp.Key}", deptKvp.Value, cacheOptions);
                    response.DepartmentNames[deptKvp.Key] = deptKvp.Value;
                }
            }
            else
            {
                _logger.LogWarning("Name lookup failed: {ErrorMessage}", authResult.ErrorMessage);
                response.Success = false;
                response.ErrorMessage = authResult.ErrorMessage;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during name lookup");
            return new NameLookupResponse
            {
                Success = false,
                ErrorMessage = "Failed to lookup names"
            };
        }
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        if (_cache.TryGetValue($"{UserCachePrefix}{userId}", out string? cachedName))
            return cachedName;

        var result = await GetNamesAsync(new List<string> { userId }, new List<string>());
        return result.UserNames.TryGetValue(userId, out string? name) ? name : null;
    }

    public async Task<string?> GetDepartmentNameAsync(string departmentId)
    {
        if (string.IsNullOrEmpty(departmentId))
            return null;

        if (_cache.TryGetValue($"{DepartmentCachePrefix}{departmentId}", out string? cachedName))
            return cachedName;

        var result = await GetNamesAsync(new List<string>(), new List<string> { departmentId });
        return result.DepartmentNames.TryGetValue(departmentId, out string? name) ? name : null;
    }

    public Task ClearUserCacheAsync(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            _cache.Remove($"{UserCachePrefix}{userId}");
            _logger.LogInformation("Cleared cache for user {UserId}", userId);
        }
        return Task.CompletedTask;
    }

    public Task ClearDepartmentCacheAsync(string departmentId)
    {
        if (!string.IsNullOrEmpty(departmentId))
        {
            _cache.Remove($"{DepartmentCachePrefix}{departmentId}");
            _logger.LogInformation("Cleared cache for department {DepartmentId}", departmentId);
        }
        return Task.CompletedTask;
    }
}
