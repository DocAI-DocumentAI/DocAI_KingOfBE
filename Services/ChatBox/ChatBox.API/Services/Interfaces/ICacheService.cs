namespace ChatBox.API.Services.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);

        Task<string?> GetStringAsync(string key);
        Task SetStringAsync(string key, string value, TimeSpan? expiration = null);

        Task<DateTime?> GetDateTimeAsync(string key);
        Task SetDateTimeAsync(string key, DateTime value, TimeSpan? expiration = null);

        // Batch operations
        Task RemoveMultipleAsync(params string[] keys);
        Task<Dictionary<string, T?>> GetMultipleAsync<T>(params string[] keys) where T : class;

        // Utility methods
        Task<bool> ExistsAsync(string key);
    }
}
