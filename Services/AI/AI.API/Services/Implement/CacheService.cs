using System.Collections.Concurrent;
using System.Text.Json;
using AI.API.Services.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace AI.API.Services.Implement
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly ConcurrentDictionary<string, byte> _cacheKeys = new();
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(10);

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> GetAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be empty", nameof(key));

            try
            {
                if (_cache.TryGetValue(key, out T cachedValue))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return await Task.FromResult(cachedValue);
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be empty", nameof(key));

            if (value == null)
            {
                _logger.LogDebug("Skipping cache set for null value, key: {Key}", key);
                return;
            }

            try
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Priority = CacheItemPriority.Normal
                };

                // Calculate size for memory limit management
                options.Size = EstimateSize(value);

                // Set sliding expiration for frequently accessed items
                if (expiration?.TotalMinutes > 30)
                {
                    options.SlidingExpiration = TimeSpan.FromMinutes(10);
                }

                // Add eviction callback
                options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, value, reason, state) =>
                    {
                        _cacheKeys.TryRemove(key.ToString(), out _);
                        _logger.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", key, reason);
                    }
                });

                _cache.Set(key, value, options);
                _cacheKeys.TryAdd(key, 0);

                _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}",
                    key, options.AbsoluteExpirationRelativeToNow);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
                // Don't throw - caching should not break the main flow
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be empty", nameof(key));

            try
            {
                _cache.Remove(key);
                _cacheKeys.TryRemove(key, out _);
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entry for key: {Key}", key);
            }
        }

        public async Task RemoveByPrefixAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Cache prefix cannot be empty", nameof(prefix));

            try
            {
                var keysToRemove = _cacheKeys.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    _cacheKeys.TryRemove(key, out _);
                }

                _logger.LogDebug("Removed {Count} cache entries with prefix: {Prefix}",
                    keysToRemove.Count, prefix);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by prefix: {Prefix}", prefix);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be empty", nameof(key));

            try
            {
                var exists = _cache.TryGetValue(key, out _);
                return await Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }
        #region Private Methods

        private long EstimateSize<T>(T value)
        {
            try
            {
                if (value is string str)
                {
                    return str.Length * 2; // 2 bytes per char
                }

                if (value is byte[] bytes)
                {
                    return bytes.Length;
                }

                // For complex objects, serialize and measure
                var json = JsonSerializer.Serialize(value);
                return json.Length * 2;
            }
            catch
            {
                // Default size estimate
                return 1024;
            }
        }
        #endregion
    }
}