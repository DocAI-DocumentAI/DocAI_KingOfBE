namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Redis service interface for Document API
    /// Focuses on Google Drive token management and caching
    /// </summary>
    public interface IRedisService
    {
        // Basic Redis operations
        Task<string> GetStringAsync(string key);
        Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);
        Task<bool> KeyExistsAsync(string key);
        Task<bool> RemoveKeyAsync(string key);

        // Google Drive Company Token Management
        Task SetGoogleDriveCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt);
        Task<(string accessToken, string refreshToken, DateTime expiresAt)?> GetGoogleDriveCompanyTokensAsync();
        Task ClearGoogleDriveCompanyTokensAsync();

        // User Google Token Management (integrates with Auth service)
        Task<(string accessToken, string refreshToken)?> GetUserGoogleTokensAsync(string userId);
        Task<bool> HasUserGoogleTokensAsync(string userId);

        // Folder cache management
        Task SetFolderCacheAsync(string cacheKey, string folderId, TimeSpan? expiry = null);
        Task<string> GetFolderCacheAsync(string cacheKey);
        Task ClearFolderCacheAsync();

        // Migration status tracking
        Task SetMigrationStatusAsync(string status, object data = null);
        Task<string> GetMigrationStatusAsync();
    }
}
