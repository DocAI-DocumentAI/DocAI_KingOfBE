using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auth.API.Services.Interface;
using StackExchange.Redis;


namespace DOCA.API.Services.Implement;

public class RedisService : IRedisService
{
    private readonly IDatabase _db;
    public RedisService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }
    public async Task<string> GetStringAsync(string key)
    {
        return await _db.StringGetAsync(key);
    }

    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        return await _db.StringSetAsync(key, value, expiry);
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }

    public async Task<bool> RemoveKeyAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    public async Task PushToListAsync(string key, string value)
    {
        await _db.ListRightPushAsync(key, value);
    }

    public async Task RemoveFromListAsync(string key, string value)
    {
        await _db.ListRemoveAsync(key, value);
    }

    public Task<List<string>> GetListAsync(string key)
    {
        return _db.ListRangeAsync(key).ContinueWith(t => t.Result.Select(x => x.ToString()).ToList());
    }

    public async Task BlacklistJwtAsync(string jti, TimeSpan expiration)
    {
        var key = $"blacklist:jwt:{jti}";
        await SetStringAsync(key, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), expiration);
    }

    public async Task<bool> IsJwtBlacklistedAsync(string jti)
    {
        var key = $"blacklist:jwt:{jti}";
        var result = await GetStringAsync(key);
        return !string.IsNullOrEmpty(result);
    }

    public async Task<bool> CheckRateLimitAsync(string key, int limit, TimeSpan window)
    {
        var current = await GetStringAsync(key);
        var count = string.IsNullOrEmpty(current) ? 0 : int.Parse(current);

        if (count >= limit)
            return false;

        if (count == 0)
            await SetStringAsync(key, "1", window);
        else
            await _db.StringIncrementAsync(key);

        return true;
    }
}
