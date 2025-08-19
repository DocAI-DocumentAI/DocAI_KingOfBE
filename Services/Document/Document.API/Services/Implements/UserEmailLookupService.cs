using Document.API.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.DTOs;

namespace Document.API.Services.Implements;

/// <summary>
/// Service for looking up user emails with caching and RabbitMQ
/// </summary>
public class UserEmailLookupService : IUserEmailLookupService
{
    private readonly IRequestClient<UserEmailRequest> _requestClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserEmailLookupService> _logger;
    
    private const int CacheExpirationMinutes = 30;
    private const string UserEmailCachePrefix = "user_email_";

    public UserEmailLookupService(
        IRequestClient<UserEmailRequest> requestClient,
        IMemoryCache cache,
        ILogger<UserEmailLookupService> logger)
    {
        _requestClient = requestClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetUserEmailsAsync(List<string> userIds)
    {
        try
        {
            var result = new Dictionary<string, string>();
            
            if (!userIds.Any())
                return result;

            // Filter out IDs that are already cached
            var uncachedUserIds = userIds.Where(id => !string.IsNullOrEmpty(id) && !_cache.TryGetValue($"{UserEmailCachePrefix}{id}", out _)).ToList();

            // Get cached emails
            foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                if (_cache.TryGetValue($"{UserEmailCachePrefix}{userId}", out string? cachedEmail) && cachedEmail != null)
                {
                    result[userId] = cachedEmail;
                }
            }

            // If all emails are cached, return immediately
            if (!uncachedUserIds.Any())
            {
                return result;
            }

            _logger.LogInformation("Requesting emails for {Count} uncached users", uncachedUserIds.Count);

            // Request emails for uncached users (one by one for now, can be optimized later)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(15)
            };

            foreach (var userId in uncachedUserIds)
            {
                try
                {
                    var request = new UserEmailRequest
                    {
                        UserId = userId,
                        RequestId = Guid.NewGuid().ToString()
                    };

                    var response = await _requestClient.GetResponse<UserEmailResponse>(request, timeout: TimeSpan.FromSeconds(2));
                    var emailResult = response.Message;

                    if (emailResult.Success && !string.IsNullOrEmpty(emailResult.Email))
                    {
                        _cache.Set($"{UserEmailCachePrefix}{userId}", emailResult.Email, cacheOptions);
                        result[userId] = emailResult.Email;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get email for user {UserId}: {ErrorMessage}", userId, emailResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting email for user {UserId}", userId);
                }
            }

            _logger.LogInformation("Successfully retrieved {Count} user emails", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user email lookup");
            return new Dictionary<string, string>();
        }
    }

    public async Task<string?> GetUserEmailAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        if (_cache.TryGetValue($"{UserEmailCachePrefix}{userId}", out string? cachedEmail))
            return cachedEmail;

        var result = await GetUserEmailsAsync(new List<string> { userId });
        return result.TryGetValue(userId, out string? email) ? email : null;
    }

    public Task ClearUserEmailCacheAsync(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            _cache.Remove($"{UserEmailCachePrefix}{userId}");
            _logger.LogInformation("Cleared email cache for user {UserId}", userId);
        }
        return Task.CompletedTask;
    }

    public Task ClearAllUserEmailCacheAsync()
    {
        // Note: IMemoryCache doesn't have a clear all method, so we'd need to track keys
        // For now, we'll just log this action
        _logger.LogInformation("Clear all user email cache requested (not implemented)");
        return Task.CompletedTask;
    }
}
