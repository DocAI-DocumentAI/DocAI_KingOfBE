using ChatBox.API.Services.Implement;

namespace ChatBox.API.Services.Interfaces
{
    public interface ICacheService
    {

        #region Basic Cache Operations
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        #endregion

        #region String Operations
        Task<string?> GetStringAsync(string key);
        Task SetStringAsync(string key, string value, TimeSpan? expiration = null);
        #endregion

        #region DateTime Operations
        Task<DateTime?> GetDateTimeAsync(string key);
        Task SetDateTimeAsync(string key, DateTime value, TimeSpan? expiration = null);
        #endregion

        #region Batch Operations
        Task RemoveMultipleAsync(params string[] keys);
        Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys) where T : class;
        #endregion

        #region Utility Methods
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Cache-aside pattern implementation. Gets from cache or executes factory and caches result.
        /// </summary>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        #endregion

        #region Health & Monitoring
        /// <summary>
        /// Get cache health status for monitoring
        /// </summary>
        Task<CacheHealthStatus> GetHealthStatusAsync();

        /// <summary>
        /// Get cache statistics including circuit breaker status
        /// </summary>
        CacheStatistics GetStatistics();
        #endregion
    }
}
