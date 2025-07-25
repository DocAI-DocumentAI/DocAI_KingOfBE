using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Auth.API.Services.Interface;

public interface IRedisService
{
    Task<string> GetStringAsync(string key);
    Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> KeyExistsAsync(string key);
    Task<bool> RemoveKeyAsync(string key);
    Task PushToListAsync(string key, string value);

    Task RemoveFromListAsync(string key, string value);

    Task<List<string>> GetListAsync(string key);

    // Google Token Management
    Task SetGoogleTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiry);
    Task<(string accessToken, string refreshToken)?> GetGoogleTokensAsync(string userId);
    Task RemoveGoogleTokensAsync(string userId);
    Task ClearGoogleTokensAsync(string userId);

    // DocAI Token Management  
    Task SetDocAITokensAsync(string userId, string accessToken, string refreshToken);
    Task<(string accessToken, string refreshToken)?> GetDocAITokensAsync(string userId);
    Task RemoveDocAITokensAsync(string userId);
    Task ClearDocAITokensAsync(string userId);

    // JWT Blacklist và Rate Limiting
    Task BlacklistJwtAsync(string jti, TimeSpan expiration);
    Task<bool> IsJwtBlacklistedAsync(string jti);
    Task<bool> CheckRateLimitAsync(string key, int limit, TimeSpan window);

    // Token cleanup
    Task ClearAllUserTokensAsync(string userId);
}
