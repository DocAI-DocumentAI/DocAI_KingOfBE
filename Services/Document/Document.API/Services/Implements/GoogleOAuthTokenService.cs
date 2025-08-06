using Document.API.Services.Interfaces;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;

namespace Document.API.Services.Implements
{
    public class GoogleOAuthTokenService : IGoogleOAuthTokenService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GoogleOAuthTokenService> _logger;

        public GoogleOAuthTokenService(IUnitOfWork unitOfWork, ILogger<GoogleOAuthTokenService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<(string accessToken, string refreshToken, DateTime expiresAt)?> GetCompanyTokensAsync()
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetCompanyTokenType();
                var token = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                if (token == null)
                {
                    _logger.LogWarning("No company tokens found in database, Instance: {InstanceId}", Environment.MachineName);
                    return null;
                }

                _logger.LogDebug("Company tokens retrieved from database. Expires at: {ExpiresAt}, Instance: {InstanceId}",
                    token.ExpiresAt, Environment.MachineName);

                return (token.AccessToken, token.RefreshToken, token.ExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company tokens from database, Instance: {InstanceId}", Environment.MachineName);
                return null;
            }
        }

        public async Task SetCompanyTokensAsync(string accessToken, string refreshToken, DateTime expiresAt)
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetCompanyTokenType();
                var existingToken = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                var now = DateTime.UtcNow;

                if (existingToken != null)
                {
                    // Update existing token
                    existingToken.AccessToken = accessToken;
                    existingToken.RefreshToken = refreshToken;
                    existingToken.ExpiresAt = expiresAt;
                    existingToken.UpdatedAt = now;

                    await _unitOfWork.GetRepository<GoogleOAuthToken>().UpdateAsync(existingToken);
                }
                else
                {
                    // Create new token
                    var newToken = new GoogleOAuthToken
                    {
                        TokenType = tokenType,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        CreatedAt = now,
                        UpdatedAt = now,
                        UserEmail = "s3rcc.9@gmail.com", // Company account
                        Scope = "https://www.googleapis.com/auth/drive"
                    };

                    await _unitOfWork.GetRepository<GoogleOAuthToken>().InsertAsync(newToken);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Company tokens stored successfully in database. Token expires at: {TokenExpiry}, Instance: {InstanceId}",
                    expiresAt, Environment.MachineName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing company tokens to database, Instance: {InstanceId}", Environment.MachineName);
                throw;
            }
        }

        public async Task DeleteCompanyTokensAsync()
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetCompanyTokenType();
                var token = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                if (token != null)
                {
                    _unitOfWork.GetRepository<GoogleOAuthToken>().DeleteAsync(token);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Company tokens deleted from database, Instance: {InstanceId}", Environment.MachineName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting company tokens from database, Instance: {InstanceId}", Environment.MachineName);
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken)?> GetUserTokensAsync(string userEmail)
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetUserTokenType(userEmail);
                var token = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                if (token == null)
                {
                    _logger.LogDebug("No user tokens found for {UserEmail} in database, Instance: {InstanceId}",
                        userEmail, Environment.MachineName);
                    return null;
                }

                return (token.AccessToken, token.RefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user tokens for {UserEmail} from database, Instance: {InstanceId}",
                    userEmail, Environment.MachineName);
                return null;
            }
        }

        public async Task SetUserTokensAsync(string userEmail, string accessToken, string refreshToken)
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetUserTokenType(userEmail);
                var existingToken = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                var now = DateTime.UtcNow;

                if (existingToken != null)
                {
                    existingToken.AccessToken = accessToken;
                    existingToken.RefreshToken = refreshToken;
                    existingToken.UpdatedAt = now;

                    await _unitOfWork.GetRepository<GoogleOAuthToken>().UpdateAsync(existingToken);
                }
                else
                {
                    var newToken = new GoogleOAuthToken
                    {
                        TokenType = tokenType,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = DateTime.UtcNow.AddYears(1), // User tokens don't typically expire
                        CreatedAt = now,
                        UpdatedAt = now,
                        UserEmail = userEmail,
                        Scope = "https://www.googleapis.com/auth/drive.readonly"
                    };

                    await _unitOfWork.GetRepository<GoogleOAuthToken>().InsertAsync(newToken);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("User tokens stored successfully for {UserEmail} in database, Instance: {InstanceId}",
                    userEmail, Environment.MachineName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing user tokens for {UserEmail} to database, Instance: {InstanceId}",
                    userEmail, Environment.MachineName);
                throw;
            }
        }

        public async Task DeleteUserTokensAsync(string userEmail)
        {
            try
            {
                var tokenType = GoogleOAuthToken.GetUserTokenType(userEmail);
                var token = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .SingleOrDefaultAsync(predicate: t => t.TokenType == tokenType);

                if (token != null)
                {
                    _unitOfWork.GetRepository<GoogleOAuthToken>().DeleteAsync(token);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("User tokens deleted for {UserEmail} from database, Instance: {InstanceId}",
                        userEmail, Environment.MachineName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user tokens for {UserEmail} from database, Instance: {InstanceId}",
                    userEmail, Environment.MachineName);
                throw;
            }
        }

        public async Task<bool> AreCompanyTokensValidAsync()
        {
            var tokens = await GetCompanyTokensAsync();
            if (tokens == null) return false;

            // Check if tokens expire within the next 5 minutes
            return tokens.Value.expiresAt > DateTime.UtcNow.AddMinutes(5);
        }

        public async Task<List<string>> GetExpiredUserTokensAsync()
        {
            try
            {
                var expiredTokens = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .GetListAsync(
                        predicate: t => t.TokenType.StartsWith("user:") && t.ExpiresAt < DateTime.UtcNow,
                        selector: t => t.UserEmail!);

                return expiredTokens.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired user tokens from database, Instance: {InstanceId}", Environment.MachineName);
                return new List<string>();
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                var expiredTokens = await _unitOfWork.GetRepository<GoogleOAuthToken>()
                    .GetListAsync(predicate: t => t.TokenType.StartsWith("user:") && t.ExpiresAt < DateTime.UtcNow.AddDays(-7)); // Keep for 7 days after expiry

                if (expiredTokens.Count > 0)
                {
                    _unitOfWork.GetRepository<GoogleOAuthToken>().DeleteRangeAsync(expiredTokens);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Cleaned up {Count} expired user tokens from database, Instance: {InstanceId}",
                        expiredTokens.Count, Environment.MachineName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens from database, Instance: {InstanceId}", Environment.MachineName);
            }
        }
    }
}
