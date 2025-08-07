using System.Text.Json;
using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace ChatBox.API.Services.Implement
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<CacheService> _logger;

        // Circuit breaker state
        private bool _isCircuitBreakerOpen = false;
        private DateTime _circuitBreakerLastFailTime = DateTime.MinValue;
        private const int CIRCUIT_BREAKER_THRESHOLD_SECONDS = 30;

        public CacheService(IDistributedCache distributedCache, ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache;
            _logger = logger;
        }

        #region Basic Cache Operations

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (IsCircuitBreakerOpen()) return null;

            try
            {
                var json = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(json))
                    return null;

                var result = JsonSerializer.Deserialize<T>(json);
                ResetCircuitBreaker(); // Success - reset circuit breaker
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON deserialization failed for cache key: {Key}", key);
                return null; // Bad data in cache - return null, don't break app
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
                TriggerCircuitBreaker();
                return null; // Always return null on error, never throw
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (IsCircuitBreakerOpen()) return;

            try
            {
                var json = JsonSerializer.Serialize(value);
                var options = new DistributedCacheEntryOptions();

                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Default 30 min

                await _distributedCache.SetStringAsync(key, json, options);
                ResetCircuitBreaker(); // Success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache SET failed for key: {Key}", key);
                TriggerCircuitBreaker();
                // Don't throw - cache failure shouldn't break app
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (IsCircuitBreakerOpen()) return;

            try
            {
                await _distributedCache.RemoveAsync(key);
                ResetCircuitBreaker(); // Success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key);
                TriggerCircuitBreaker();
                // Don't throw - cache failure shouldn't break app
            }
        }

        #endregion

        #region String Operations

        public async Task<string?> GetStringAsync(string key)
        {
            if (IsCircuitBreakerOpen()) return null;

            try
            {
                var result = await _distributedCache.GetStringAsync(key);
                ResetCircuitBreaker(); // Success
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache GET STRING failed for key: {Key}", key);
                TriggerCircuitBreaker();
                return null;
            }
        }

        public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null)
        {
            if (IsCircuitBreakerOpen()) return;

            try
            {
                var options = new DistributedCacheEntryOptions();
                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                await _distributedCache.SetStringAsync(key, value, options);
                ResetCircuitBreaker(); // Success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache SET STRING failed for key: {Key}", key);
                TriggerCircuitBreaker();
            }
        }

        #endregion

        #region DateTime Operations

        public async Task<DateTime?> GetDateTimeAsync(string key)
        {
            if (IsCircuitBreakerOpen()) return null;

            try
            {
                var dateStr = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(dateStr))
                    return null;

                if (DateTime.TryParse(dateStr, out var result))
                {
                    ResetCircuitBreaker(); // Success
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache GET DATETIME failed for key: {Key}", key);
                TriggerCircuitBreaker();
                return null;
            }
        }

        public async Task SetDateTimeAsync(string key, DateTime value, TimeSpan? expiration = null)
        {
            if (IsCircuitBreakerOpen()) return;

            try
            {
                var options = new DistributedCacheEntryOptions();

                if (expiration.HasValue)
                    options.SetAbsoluteExpiration(expiration.Value);
                else
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                await _distributedCache.SetStringAsync(key, value.ToString("O"), options); // ISO format
                ResetCircuitBreaker(); // Success
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache SET DATETIME failed for key: {Key}", key);
                TriggerCircuitBreaker();
            }
        }

        #endregion

        #region Batch Operations

        public async Task RemoveMultipleAsync(params string[] keys)
        {
            if (IsCircuitBreakerOpen()) return;
            if (keys == null || keys.Length == 0) return;

            // Process in parallel but handle individual failures gracefully
            var tasks = keys.Select(async key =>
            {
                try
                {
                    await _distributedCache.RemoveAsync(key);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove cache key: {Key}", key);
                    return false;
                }
            });

            try
            {
                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);

                if (successCount > 0)
                {
                    ResetCircuitBreaker(); // At least some succeeded
                }

                if (successCount < keys.Length)
                {
                    _logger.LogWarning("Removed {SuccessCount}/{TotalCount} cache keys",
                        successCount, keys.Length);

                    if (successCount == 0)
                    {
                        TriggerCircuitBreaker(); // All failed
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch cache removal failed completely");
                TriggerCircuitBreaker();
            }
        }

        public async Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys) where T : class
        {
            var result = new Dictionary<string, T?>();

            if (IsCircuitBreakerOpen() || keys == null || keys.Length == 0)
                return result;

            var tasks = keys.Select(async key => new
            {
                Key = key,
                Value = await GetAsync<T>(key) // Uses existing error handling
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
                _logger.LogWarning(ex, "Batch cache retrieval failed");
                // Return partial results
            }

            return result;
        }

        #endregion

        #region Utility Methods

        public async Task<bool> ExistsAsync(string key)
        {
            if (IsCircuitBreakerOpen()) return false;

            try
            {
                var value = await _distributedCache.GetStringAsync(key);
                var exists = !string.IsNullOrEmpty(value);
                ResetCircuitBreaker(); // Success
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache EXISTS check failed for key: {Key}", key);
                TriggerCircuitBreaker();
                return false;
            }
        }

        /// <summary>
        /// Generic cache-aside pattern implementation
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            // Try to get from cache first
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT for key: {Key}", key);
                return cached;
            }

            _logger.LogDebug("Cache MISS for key: {Key}, executing factory", key);

            // Cache miss - execute factory
            var result = await factory();

            if (result != null)
            {
                // Fire and forget cache set (don't let cache failures block the response)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SetAsync(key, result, expiration);
                        _logger.LogDebug("Cached result for key: {Key}", key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache result for key: {Key}", key);
                    }
                });
            }

            return result;
        }

        #endregion

        #region Circuit Breaker Pattern

        private bool IsCircuitBreakerOpen()
        {
            if (!_isCircuitBreakerOpen) return false;

            // Check if circuit breaker should be reset
            var timeSinceLastFailure = DateTime.UtcNow - _circuitBreakerLastFailTime;
            if (timeSinceLastFailure.TotalSeconds > CIRCUIT_BREAKER_THRESHOLD_SECONDS)
            {
                _logger.LogInformation("Circuit breaker reset after {Seconds} seconds",
                    CIRCUIT_BREAKER_THRESHOLD_SECONDS);
                _isCircuitBreakerOpen = false;
                return false;
            }

            return true;
        }

        private void TriggerCircuitBreaker()
        {
            if (!_isCircuitBreakerOpen)
            {
                _logger.LogWarning("Cache circuit breaker OPENED due to repeated failures");
                _isCircuitBreakerOpen = true;
            }
            _circuitBreakerLastFailTime = DateTime.UtcNow;
        }

        private void ResetCircuitBreaker()
        {
            if (_isCircuitBreakerOpen)
            {
                _logger.LogInformation("Cache circuit breaker CLOSED - operations successful");
                _isCircuitBreakerOpen = false;
            }
        }

        #endregion

        #region Health & Monitoring

        /// <summary>
        /// Get cache health status
        /// </summary>
        public async Task<CacheHealthStatus> GetHealthStatusAsync()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Test basic operations
                var testKey = $"health_check_{Guid.NewGuid()}";
                var testValue = "test";

                await SetStringAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                var retrieved = await GetStringAsync(testKey);
                await RemoveAsync(testKey);

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var isHealthy = retrieved == testValue;

                return new CacheHealthStatus
                {
                    IsHealthy = isHealthy,
                    ResponseTimeMs = (int)responseTime,
                    IsCircuitBreakerOpen = _isCircuitBreakerOpen,
                    LastFailureTime = _circuitBreakerLastFailTime == DateTime.MinValue ? null : _circuitBreakerLastFailTime,
                    CheckTime = DateTime.UtcNow,
                    Message = isHealthy ? "Cache is operating normally" : "Cache test failed"
                };
            }
            catch (Exception ex)
            {
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return new CacheHealthStatus
                {
                    IsHealthy = false,
                    ResponseTimeMs = (int)responseTime,
                    IsCircuitBreakerOpen = _isCircuitBreakerOpen,
                    LastFailureTime = _circuitBreakerLastFailTime == DateTime.MinValue ? null : _circuitBreakerLastFailTime,
                    CheckTime = DateTime.UtcNow,
                    Message = $"Cache health check failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                IsCircuitBreakerOpen = _isCircuitBreakerOpen,
                LastFailureTime = _circuitBreakerLastFailTime == DateTime.MinValue ? null : _circuitBreakerLastFailTime,
                CircuitBreakerThresholdSeconds = CIRCUIT_BREAKER_THRESHOLD_SECONDS
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class CacheHealthStatus
    {
        public bool IsHealthy { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool IsCircuitBreakerOpen { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime CheckTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CacheStatistics
    {
        public bool IsCircuitBreakerOpen { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public int CircuitBreakerThresholdSeconds { get; set; }
    }

    #endregion
}

