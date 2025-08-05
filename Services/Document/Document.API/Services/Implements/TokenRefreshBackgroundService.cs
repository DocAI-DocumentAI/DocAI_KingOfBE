using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Background service that proactively refreshes Google Drive OAuth tokens to prevent expiration
    /// </summary>
    public class TokenRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TokenRefreshBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes

        public TokenRefreshBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<TokenRefreshBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Token refresh background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshTokensIfNeeded();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during background token refresh");
                }

                // Wait for next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Token refresh background service stopped");
        }

        private async Task RefreshTokensIfNeeded()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var oauthService = scope.ServiceProvider.GetRequiredService<IGoogleDriveOAuthService>();

                // This will automatically refresh tokens if they're expiring
                var hasValidTokens = await oauthService.HasValidCompanyTokensAsync();

                if (hasValidTokens)
                {
                    _logger.LogDebug("Company tokens are valid, no refresh needed");
                }
                else
                {
                    _logger.LogWarning("Company tokens are not valid or missing");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check/refresh company tokens in background service");
            }
        }
    }
}
