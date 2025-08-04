using Google.Apis.Drive.v3;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for managing Google Drive OAuth2 authentication and token management
    /// Integrates with existing GoogleOAuthService from Auth microservice
    /// </summary>
    public interface IGoogleDriveOAuthService
    {
        /// <summary>
        /// Create DriveService instance using company account OAuth tokens
        /// </summary>
        /// <returns>Authenticated DriveService for company account</returns>
        Task<DriveService> CreateCompanyDriveServiceAsync();

        /// <summary>
        /// Create DriveService instance using user's personal OAuth tokens
        /// </summary>
        /// <param name="userId">User ID to get tokens for</param>
        /// <returns>Authenticated DriveService for user account</returns>
        Task<DriveService> CreateUserDriveServiceAsync(string userId);

        /// <summary>
        /// Get company account OAuth tokens (stored securely)
        /// </summary>
        /// <returns>Company account access token</returns>
        Task<string> GetCompanyAccessTokenAsync();

        /// <summary>
        /// Get user's OAuth tokens from existing auth service
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User's access token</returns>
        Task<string> GetUserAccessTokenAsync(string userId);

        /// <summary>
        /// Refresh company account tokens if needed
        /// </summary>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshCompanyTokensAsync();

        /// <summary>
        /// Refresh user tokens if needed
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshUserTokensAsync(string userId);

        /// <summary>
        /// Store company account OAuth tokens securely
        /// This should be called once during setup with company account authorization
        /// </summary>
        /// <param name="accessToken">Company account access token</param>
        /// <param name="refreshToken">Company account refresh token</param>
        /// <param name="expiresAt">Token expiration time</param>
        Task StoreCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt);

        /// <summary>
        /// Check if company account tokens are available and valid
        /// </summary>
        /// <returns>True if company tokens are available</returns>
        Task<bool> HasValidCompanyTokensAsync();

        /// <summary>
        /// Check if user has valid Google Drive tokens
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if user has valid tokens</returns>
        Task<bool> HasValidUserTokensAsync(string userId);
    }
}
