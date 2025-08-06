namespace Document.API.Services.Interfaces
{
    public interface IGoogleOAuthTokenService
    {
        // Company token methods
        Task<(string accessToken, string refreshToken, DateTime expiresAt)?> GetCompanyTokensAsync();
        Task SetCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt);
        Task DeleteCompanyTokensAsync();

        // User token methods  
        Task<(string accessToken, string refreshToken)?> GetUserTokensAsync(string userEmail);
        Task SetUserTokensAsync(string userEmail, string accessToken, string refreshToken);
        Task DeleteUserTokensAsync(string userEmail);

        // Utility methods
        Task<bool> AreCompanyTokensValidAsync();
        Task<List<string>> GetExpiredUserTokensAsync();
        Task CleanupExpiredTokensAsync();
    }
}
