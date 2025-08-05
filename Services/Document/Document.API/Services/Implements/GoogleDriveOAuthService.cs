using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Document.API.Configuration;
using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Google Drive OAuth service for personal Gmail account integration
    /// Manages both company account and user account OAuth tokens
    /// </summary>
    public class GoogleDriveOAuthService : IGoogleDriveOAuthService
    {
        private readonly GoogleDriveConfiguration _config;
        private readonly ILogger<GoogleDriveOAuthService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IRedisService _redisService;

        public GoogleDriveOAuthService(
            IOptions<GoogleDriveConfiguration> config,
            ILogger<GoogleDriveOAuthService> logger,
            HttpClient httpClient,
            IConfiguration configuration,
            IRedisService redisService)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
            _redisService = redisService;

            // Use existing GoogleOAuth configuration if not specified
            if (string.IsNullOrEmpty(_config.ClientId))
            {
                _config.ClientId = _configuration["GoogleOAuth:ClientId"];
            }
            if (string.IsNullOrEmpty(_config.ClientSecret))
            {
                _config.ClientSecret = _configuration["GoogleOAuth:ClientSecret"];
            }
        }

        public async Task<DriveService> CreateCompanyDriveServiceAsync()
        {
            try
            {
                var accessToken = await GetCompanyAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Company account Google Drive tokens not available. Please authorize the company account first.");
                }

                var credential = GoogleCredential.FromAccessToken(accessToken);
                
                return new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = _config.ApplicationName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create company Drive service");
                throw;
            }
        }

        public async Task<DriveService> CreateUserDriveServiceAsync(string userId)
        {
            try
            {
                var accessToken = await GetUserAccessTokenAsync(userId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException($"User {userId} does not have valid Google Drive tokens");
                }

                var credential = GoogleCredential.FromAccessToken(accessToken);
                
                return new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = _config.ApplicationName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user Drive service for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> GetCompanyAccessTokenAsync()
        {
            try
            {
                var tokens = await _redisService.GetGoogleDriveCompanyTokensAsync();

                if (tokens == null)
                {
                    _logger.LogWarning("No company tokens found in storage");
                    return null;
                }

                // Check if token is expired and refresh if needed
                if (tokens.Value.expiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Company token is expiring, attempting refresh");
                    var refreshed = await RefreshCompanyTokensAsync();
                    if (refreshed)
                    {
                        // Get the refreshed tokens
                        tokens = await _redisService.GetGoogleDriveCompanyTokensAsync();
                        if (tokens != null)
                        {
                            _logger.LogInformation("Successfully refreshed and retrieved new company access token");
                            return tokens.Value.accessToken;
                        }
                    }

                    _logger.LogWarning("Failed to refresh company tokens, returning expired token");
                    return null;
                }

                return tokens.Value.accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company access token");
                return null;
            }
        }

        public async Task<string> GetUserAccessTokenAsync(string userId)
        {
            try
            {
                var tokens = await _redisService.GetUserGoogleTokensAsync(userId);

                if (tokens == null)
                {
                    _logger.LogWarning("No Google tokens found for user {UserId}", userId);
                    return null;
                }

                // Note: User token refresh should be handled by Auth service
                // For now, just return the access token
                return tokens.Value.accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user access token for {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> RefreshCompanyTokensAsync()
        {
            try
            {
                var tokens = await _redisService.GetGoogleDriveCompanyTokensAsync();
                if (tokens?.refreshToken == null)
                {
                    _logger.LogError("No refresh token available for company account");
                    return false;
                }

                var refreshedTokens = await RefreshOAuthTokensAsync(tokens.Value.refreshToken);
                if (refreshedTokens != null)
                {
                    await _redisService.SetGoogleDriveCompanyTokensAsync(
                        refreshedTokens.AccessToken,
                        refreshedTokens.RefreshToken ?? tokens.Value.refreshToken,
                        refreshedTokens.ExpiresAt);

                    _logger.LogInformation("Company tokens refreshed successfully");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing company tokens");
                return false;
            }
        }

        public async Task<bool> RefreshUserTokensAsync(string userId)
        {
            try
            {
                // This should integrate with existing GoogleOAuthService.RefreshGoogleTokenAsync
                // For now, placeholder implementation
                _logger.LogInformation("Refreshing tokens for user {UserId}", userId);
                
                // You would call the existing Auth microservice or GoogleOAuthService here
                // return await _googleOAuthService.RefreshGoogleTokenAsync(userRefreshToken);
                
                return false; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing user tokens for {UserId}", userId);
                return false;
            }
        }

        public async Task StoreCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt)
        {
            try
            {
                await _redisService.SetGoogleDriveCompanyTokensAsync(accessToken, refreshToken, expiresAt);
                _logger.LogInformation("Company tokens stored successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing company tokens");
                throw;
            }
        }

        public async Task<bool> HasValidCompanyTokensAsync()
        {
            try
            {
                var tokens = await _redisService.GetGoogleDriveCompanyTokensAsync();
                if (tokens == null)
                {
                    _logger.LogWarning("No company tokens found in storage");
                    return false;
                }

                // If tokens are expired or expiring soon, try to refresh them
                if (tokens.Value.expiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Company tokens are expired or expiring soon, attempting refresh");
                    var refreshed = await RefreshCompanyTokensAsync();
                    if (refreshed)
                    {
                        _logger.LogInformation("Company tokens refreshed successfully");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh company tokens");
                        return false;
                    }
                }

                // Tokens are still valid
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking company token validity");
                return false;
            }
        }

        public async Task<bool> HasValidUserTokensAsync(string userId)
        {
            try
            {
                return await _redisService.HasUserGoogleTokensAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user token validity for {UserId}", userId);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task<RefreshedTokenData> RefreshOAuthTokensAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Attempting to refresh Google OAuth tokens");

                var tokenRequest = new Dictionary<string, string>
                {
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                };

                var content = new FormUrlEncodedContent(tokenRequest);
                var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token refresh failed with status {StatusCode}: {Error}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Token refresh response received: {Response}", jsonResponse);

                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                var refreshedData = new RefreshedTokenData
                {
                    AccessToken = tokenResponse.GetProperty("access_token").GetString(),
                    RefreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken, // Use original if new one not provided
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.GetProperty("expires_in").GetInt32())
                };

                _logger.LogInformation("Successfully refreshed OAuth tokens, expires at: {ExpiresAt}", refreshedData.ExpiresAt);
                return refreshedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing OAuth tokens");
                return null;
            }
        }

        #endregion

        #region Data Models

        private class RefreshedTokenData
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        #endregion
    }
}
