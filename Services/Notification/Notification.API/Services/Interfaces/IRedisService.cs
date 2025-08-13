namespace Notification.API.Services.Interfaces;

public interface IRedisService
{
    Task<string> GetStringAsync(string key);
    Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> KeyExistsAsync(string key);
    Task<bool> RemoveKeyAsync(string key);
    Task PushToListAsync(string key, string value);

    Task RemoveFromListAsync(string key, string value);

    Task<List<string>> GetListAsync(string key);
    Task<bool> CheckRateLimitAsync(string key, int limit, TimeSpan window);
}