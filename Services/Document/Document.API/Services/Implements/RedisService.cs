using StackExchange.Redis;
using System.Text.Json;
using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Redis service implementation for Document API
    /// Handles Google Drive tokens and caching
    /// </summary>
    public class RedisService : IRedisService
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisService> _logger;

        // Key prefixes for Document API
        private const string GOOGLE_DRIVE_COMPANY_TOKENS_KEY = "google_drive_company_tokens";
        private const string GOOGLE_TOKENS_PREFIX = "google_tokens:"; // Same as Auth service
        private const string FOLDER_CACHE_PREFIX = "google_drive_folders:";
        private const string MIGRATION_STATUS_KEY = "google_drive_migration_status";

        public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;

            // Log Redis connection info for debugging
            var endpoints = redis.GetEndPoints();
            _logger.LogInformation("Redis connection initialized. Endpoints: {Endpoints}, Instance: {InstanceId}",
                string.Join(", ", endpoints.Select(e => e.ToString())), Environment.MachineName);
        }

        public async Task<string> GetStringAsync(string key)
        {
            try
            {
                return await _database.StringGetAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting string from Redis with key: {Key}", key);
                return null;
            }
        }

        public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                return await _database.StringSetAsync(key, value, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting string in Redis with key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking key existence in Redis: {Key}", key);
                return false;
            }
        }

        public async Task<bool> RemoveKeyAsync(string key)
        {
            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key from Redis: {Key}", key);
                return false;
            }
        }

        public async Task SetGoogleDriveCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt)
        {
            try
            {
                var tokenData = new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    StoredAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(tokenData);
                var ttl = expiresAt - DateTime.UtcNow;

                // Store with TTL longer than token expiry to allow for refresh attempts
                // Add 2 hours buffer to ensure tokens are available for refresh
                var expiry = ttl > TimeSpan.Zero ? ttl.Add(TimeSpan.FromHours(2)) : TimeSpan.FromDays(1);
                var success = await _database.StringSetAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY, json, expiry);

                _logger.LogInformation("Google Drive company tokens stored successfully. Token expires at: {TokenExpiry}, Redis TTL: {RedisTTL} minutes, Redis Set Success: {Success}, Instance: {InstanceId}",
                    expiresAt, expiry.TotalMinutes, success, Environment.MachineName);

                // Verify the key was actually set
                var verification = await _database.KeyExistsAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY);
                _logger.LogInformation("Token storage verification - Key exists after set: {KeyExists}, Instance: {InstanceId}", verification, Environment.MachineName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store Google Drive company tokens");
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken, DateTime expiresAt)?> GetGoogleDriveCompanyTokensAsync()
        {
            try
            {
                // Check if key exists and get TTL for debugging
                var keyExists = await _database.KeyExistsAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY);
                var ttl = await _database.KeyTimeToLiveAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY);

                _logger.LogDebug("Redis key check - Exists: {KeyExists}, TTL: {TTL} seconds, Instance: {InstanceId}", keyExists, ttl?.TotalSeconds, Environment.MachineName);

                var json = await _database.StringGetAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY);

                if (!json.HasValue)
                {
                    _logger.LogWarning("No Google Drive company tokens found in Redis. Key exists: {KeyExists}, TTL: {TTL}, Instance: {InstanceId}", keyExists, ttl?.TotalSeconds, Environment.MachineName);
                    return null;
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                var accessToken = tokenData.GetProperty("AccessToken").GetString();
                var refreshToken = tokenData.GetProperty("RefreshToken").GetString();
                var expiresAt = tokenData.GetProperty("ExpiresAt").GetDateTime();

                return (accessToken, refreshToken, expiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Google Drive company tokens");
                return null;
            }
        }

        public async Task ClearGoogleDriveCompanyTokensAsync()
        {
            try
            {
                await _database.KeyDeleteAsync(GOOGLE_DRIVE_COMPANY_TOKENS_KEY);
                _logger.LogInformation("Google Drive company tokens cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear Google Drive company tokens");
            }
        }

        public async Task<(string accessToken, string refreshToken)?> GetUserGoogleTokensAsync(string userId)
        {
            try
            {
                // Use same key format as Auth service for compatibility
                var key = GOOGLE_TOKENS_PREFIX + userId;
                var json = await _database.StringGetAsync(key);

                if (!json.HasValue)
                {
                    _logger.LogDebug("No Google tokens found for user: {UserId}", userId);
                    return null;
                }

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

        public async Task<bool> HasUserGoogleTokensAsync(string userId)
        {
            try
            {
                var key = GOOGLE_TOKENS_PREFIX + userId;
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user Google tokens for: {UserId}", userId);
                return false;
            }
        }

        public async Task SetFolderCacheAsync(string cacheKey, string folderId, TimeSpan? expiry = null)
        {
            try
            {
                var key = FOLDER_CACHE_PREFIX + cacheKey;
                var defaultExpiry = expiry ?? TimeSpan.FromHours(24); // Cache folders for 24 hours
                
                await _database.StringSetAsync(key, folderId, defaultExpiry);
                _logger.LogDebug("Folder cache set for key: {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting folder cache for key: {CacheKey}", cacheKey);
            }
        }

        public async Task<string> GetFolderCacheAsync(string cacheKey)
        {
            try
            {
                var key = FOLDER_CACHE_PREFIX + cacheKey;
                var result = await _database.StringGetAsync(key);
                
                if (result.HasValue)
                {
                    _logger.LogDebug("Folder cache hit for key: {CacheKey}", cacheKey);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder cache for key: {CacheKey}", cacheKey);
                return null;
            }
        }

        public async Task ClearFolderCacheAsync()
        {
            try
            {
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: FOLDER_CACHE_PREFIX + "*");
                
                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }
                
                _logger.LogInformation("Folder cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing folder cache");
            }
        }

        public async Task SetMigrationStatusAsync(string status, object data = null)
        {
            try
            {
                var migrationData = new
                {
                    Status = status,
                    Data = data,
                    UpdatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(migrationData);
                await _database.StringSetAsync(MIGRATION_STATUS_KEY, json);
                
                _logger.LogInformation("Migration status updated: {Status}", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting migration status: {Status}", status);
            }
        }

        public async Task<string> GetMigrationStatusAsync()
        {
            try
            {
                var json = await _database.StringGetAsync(MIGRATION_STATUS_KEY);
                
                if (!json.HasValue)
                {
                    return "not_started";
                }

                var migrationData = JsonSerializer.Deserialize<JsonElement>(json);
                return migrationData.GetProperty("Status").GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                return "error";
            }
        }

        #region Permission Caching

        public async Task SetDepartmentEmployeesAsync(string departmentId, List<string> employeeEmails, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"dept_employees:{departmentId}";
                var value = JsonSerializer.Serialize(employeeEmails);
                await _database.StringSetAsync(key, value, expiry ?? TimeSpan.FromMinutes(30));
                _logger.LogDebug("Cached department employees for {DepartmentId}", departmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching department employees for {DepartmentId}", departmentId);
            }
        }

        public async Task<List<string>?> GetDepartmentEmployeesAsync(string departmentId)
        {
            try
            {
                var key = $"dept_employees:{departmentId}";
                var value = await _database.StringGetAsync(key);
                if (value.HasValue)
                {
                    return JsonSerializer.Deserialize<List<string>>(value);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached department employees for {DepartmentId}", departmentId);
                return null;
            }
        }

        public async Task SetDepartmentManagersAsync(string departmentId, List<string> managerEmails, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"dept_managers:{departmentId}";
                var value = JsonSerializer.Serialize(managerEmails);
                await _database.StringSetAsync(key, value, expiry ?? TimeSpan.FromMinutes(30));
                _logger.LogDebug("Cached department managers for {DepartmentId}", departmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching department managers for {DepartmentId}", departmentId);
            }
        }

        public async Task<List<string>?> GetDepartmentManagersAsync(string departmentId)
        {
            try
            {
                var key = $"dept_managers:{departmentId}";
                var value = await _database.StringGetAsync(key);
                if (value.HasValue)
                {
                    return JsonSerializer.Deserialize<List<string>>(value);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached department managers for {DepartmentId}", departmentId);
                return null;
            }
        }

        public async Task SetAllCompanyEmployeesAsync(List<string> employeeEmails, TimeSpan? expiry = null)
        {
            try
            {
                var key = "company_employees:all";
                var value = JsonSerializer.Serialize(employeeEmails);
                await _database.StringSetAsync(key, value, expiry ?? TimeSpan.FromMinutes(60));
                _logger.LogDebug("Cached all company employees");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching all company employees");
            }
        }

        public async Task<List<string>?> GetAllCompanyEmployeesAsync()
        {
            try
            {
                var key = "company_employees:all";
                var value = await _database.StringGetAsync(key);
                if (value.HasValue)
                {
                    return JsonSerializer.Deserialize<List<string>>(value);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached company employees");
                return null;
            }
        }

        public async Task SetUserEmailAsync(string userId, string email, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"user_email:{userId}";
                await _database.StringSetAsync(key, email, expiry ?? TimeSpan.FromHours(24));
                _logger.LogDebug("Cached user email for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching user email for {UserId}", userId);
            }
        }

        public async Task<string?> GetUserEmailAsync(string userId)
        {
            try
            {
                var key = $"user_email:{userId}";
                var value = await _database.StringGetAsync(key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached user email for {UserId}", userId);
                return null;
            }
        }

        #endregion

        #region Cache Management

        public async Task ClearReplacementSuggestionsAsync()
        {
            try
            {
                // Clear all replacement suggestion cache keys
                var pattern = "replacement_suggestions:*";
                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);

                foreach (var key in keys)
                {
                    await _database.KeyDeleteAsync(key);
                }

                _logger.LogDebug("Cleared replacement suggestion cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing replacement suggestion cache");
            }
        }

        #endregion
    }
}
