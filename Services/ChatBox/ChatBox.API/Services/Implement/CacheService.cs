using System.Text.Json;
using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ChatBox.API.Services.Implement
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IDistributedCache distributedCache, ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var json = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions();

                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Default 30 min

                await _distributedCache.SetStringAsync(key, json, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set cache key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _distributedCache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cache key: {Key}", key);
            }
        }
        public async Task<string?> GetStringAsync(string key)
        {
            try
            {
                return await _distributedCache.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get string cache key: {Key}", key);
                return null;
            }
        }

        public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions();
                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                await _distributedCache.SetStringAsync(key, value, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set string cache key: {Key}", key);
            }
        }
        public async Task<DateTime?> GetDateTimeAsync(string key)
        {
            try
            {
                var dateStr = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(dateStr))
                    return null;

                if (DateTime.TryParse(dateStr, out var result))
                    return result;

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get DateTime cache key: {Key}", key);
                return null;
            }
        }

        public async Task SetDateTimeAsync(string key, DateTime value, TimeSpan? expiration = null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions();

                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                await _distributedCache.SetStringAsync(key, value.ToString("O"), options); // ISO format
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set DateTime cache key: {Key}", key);
            }
        }

        // 🔧 Batch operations for better performance
        public async Task RemoveMultipleAsync(params string[] keys)
        {
            var tasks = keys.Select(key => RemoveAsync(key));
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove multiple cache keys");
            }
        }

        public async Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys) where T : class
        {
            var result = new Dictionary<string, T?>();
            var tasks = keys.Select(async key => new
            {
                Key = key,
                Value = await GetAsync<T>(key)
            });

            try
            {
                var results = await Task.WhenAll(tasks);
                foreach (var item in results)
                {
                    result[item.Key] = item.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get multiple cache keys");
            }

            return result;
        }

        // 🔧 Cache statistics (optional)
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var value = await _distributedCache.GetStringAsync(key);
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check cache key existence: {Key}", key);
                return false;
            }
        }
    }
}
