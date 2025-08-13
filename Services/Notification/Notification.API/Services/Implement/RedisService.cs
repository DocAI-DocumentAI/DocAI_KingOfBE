using Notification.API.Services.Interfaces;
using StackExchange.Redis;
using System.Text.Json;
using IDatabase = StackExchange.Redis.IDatabase;

namespace Notification.API.Services.Implement;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    public async Task<string> GetStringAsync(string key)
    {
        return await _database.StringGetAsync(key);
    }

    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        return await _database.StringSetAsync(key, value, expiry);
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task<bool> RemoveKeyAsync(string key)
    {
        return await _database.KeyDeleteAsync(key);
    }

    public async Task PushToListAsync(string key, string value)
    {
        await _database.ListRightPushAsync(key, value);
    }

    public async Task RemoveFromListAsync(string key, string value)
    {
        await _database.ListRemoveAsync(key, value);
    }

    public Task<List<string>> GetListAsync(string key)
    {
        return _database.ListRangeAsync(key).ContinueWith(t => t.Result.Select(x => x.ToString()).ToList());
    }

    public async Task<bool> CheckRateLimitAsync(string key, int limit, TimeSpan window)
    {
        try
        {
            var current = await _database.StringIncrementAsync(key);

            if (current == 1)
            {
                await _database.KeyExpireAsync(key, window);
            }

            return current <= limit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check rate limit for key: {Key}", key);
            return true; // Allow on error
        }
    }
}