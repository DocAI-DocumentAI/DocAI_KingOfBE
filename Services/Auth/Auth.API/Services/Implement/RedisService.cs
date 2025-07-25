using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auth.API.Services.Interface;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DOCA.API.Services.Implement;

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;

    // Key prefixes
    private const string GOOGLE_TOKEN_PREFIX = "google_tokens:";
    private const string DOCAI_TOKEN_PREFIX = "docai_tokens:";
    private const string JWT_BLACKLIST_PREFIX = "jwt_blacklist:";
    private const string OTP_PREFIX = "otp:";

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

    public async Task BlacklistJwtAsync(string jti, TimeSpan ttl)
    {
        try
        {
            var key = JWT_BLACKLIST_PREFIX + jti;
            await _database.StringSetAsync(key, "blacklisted", ttl);
            _logger.LogInformation("JWT blacklisted: {Jti}", jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to blacklist JWT: {Jti}", jti);
        }
    }

    public async Task<bool> IsJwtBlacklistedAsync(string jti)
    {
        try
        {
            var key = JWT_BLACKLIST_PREFIX + jti;
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check JWT blacklist: {Jti}", jti);
            return false;
        }
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

    public async Task SetGoogleTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt)
    {
        try
        {
            var key = GOOGLE_TOKEN_PREFIX + userId;
            var tokenData = new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };

            var json = JsonSerializer.Serialize(tokenData);
            var ttl = expiresAt - DateTime.UtcNow;

            await _database.StringSetAsync(key, json, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromDays(7));
            _logger.LogInformation("Google tokens stored for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store Google tokens for user: {UserId}", userId);
        }
    }

    public async Task<(string accessToken, string refreshToken)?> GetGoogleTokensAsync(string userId)
    {
        try
        {
            var key = GOOGLE_TOKEN_PREFIX + userId;
            var json = await _database.StringGetAsync(key);

            if (!json.HasValue)
                return null;

            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
            var accessToken = tokenData.GetProperty("AccessToken").GetString();
            var refreshToken = tokenData.GetProperty("RefreshToken").GetString();

            return (accessToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Google tokens for user: {UserId}", userId);
            return null;
        }
    }

    public async Task ClearGoogleTokensAsync(string userId)
    {
        try
        {
            var key = GOOGLE_TOKEN_PREFIX + userId;
            await _database.KeyDeleteAsync(key);
            _logger.LogInformation("Google tokens cleared for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Google tokens for user: {UserId}", userId);
        }
    }

    public async Task SetDocAITokensAsync(string userId, string accessToken, string refreshToken)
    {
        try
        {
            var key = DOCAI_TOKEN_PREFIX + userId;
            var tokenData = new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                CreatedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(tokenData);
            await _database.StringSetAsync(key, json, TimeSpan.FromDays(7));
            _logger.LogInformation("DocAI tokens stored for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store DocAI tokens for user: {UserId}", userId);
        }
    }

    public async Task<(string accessToken, string refreshToken)?> GetDocAITokensAsync(string userId)
    {
        try
        {
            var key = DOCAI_TOKEN_PREFIX + userId;
            var json = await _database.StringGetAsync(key);

            if (!json.HasValue)
                return null;

            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
            var accessToken = tokenData.GetProperty("AccessToken").GetString();
            var refreshToken = tokenData.GetProperty("RefreshToken").GetString();

            return (accessToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get DocAI tokens for user: {UserId}", userId);
            return null;
        }
    }

    public async Task ClearDocAITokensAsync(string userId)
    {
        try
        {
            var key = DOCAI_TOKEN_PREFIX + userId;
            await _database.KeyDeleteAsync(key);
            _logger.LogInformation("DocAI tokens cleared for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear DocAI tokens for user: {UserId}", userId);
        }
    }

    public async Task ClearAllUserTokensAsync(string userId)
    {
        try
        {
            await Task.WhenAll(
                ClearGoogleTokensAsync(userId),
                ClearDocAITokensAsync(userId)
            );
            _logger.LogInformation("All tokens cleared for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all tokens for user: {UserId}", userId);
        }
    }

    public Task RemoveGoogleTokensAsync(string userId)
    {
        return ClearGoogleTokensAsync(userId);
    }

    public Task RemoveDocAITokensAsync(string userId)
    {
        return ClearDocAITokensAsync(userId);
    }
}
