using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Document.API.Configuration;
using Document.API.Services.Interfaces;
using Document.API.Attributes;
using Document.API.Constants;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for Google Drive setup and company account authorization
    /// This is used for one-time setup of the company account
    /// </summary>
    [Route(ApiEndPointConstant.GoogleApiEndpoint)]
    [ApiController]
    public class GoogleDriveSetupController : ControllerBase
    {
        private readonly IGoogleDriveOAuthService _oauthService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly GoogleDriveConfiguration _config;
        private readonly ILogger<GoogleDriveSetupController> _logger;

        public GoogleDriveSetupController(
            IGoogleDriveOAuthService oauthService,
            IGoogleDriveService googleDriveService,
            IOptions<GoogleDriveConfiguration> config,
            ILogger<GoogleDriveSetupController> logger)
        {
            _oauthService = oauthService;
            _googleDriveService = googleDriveService;
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Get Google OAuth authorization URL for company account setup
        /// This should only be used once during initial setup
        /// </summary>
        /// <returns>Authorization URL for company account</returns>
        [HttpGet(ApiEndPointConstant.GoogleDrive.CompanyAuth)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public IActionResult GetCompanyAuthUrl()
        {
            try
            {
                _logger.LogInformation("Generating company auth URL. BaseUrl: {BaseUrl}, ClientId: {ClientId}",
                    _config.BaseUrl, _config.ClientId?.Substring(0, 10) + "...");

                if (string.IsNullOrEmpty(_config.BaseUrl))
                {
                    _logger.LogError("BaseUrl is not configured in GoogleDrive settings");
                    return BadRequest("BaseUrl is not configured");
                }

                if (string.IsNullOrEmpty(_config.ClientId))
                {
                    _logger.LogError("ClientId is not configured in GoogleDrive settings");
                    return BadRequest("ClientId is not configured");
                }

                var state = Guid.NewGuid().ToString();
                var scopes = string.Join(" ", _config.Scopes);

                var redirectUri = $"{_config.BaseUrl}/api/googledrive-setup/company-callback";
                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                             $"client_id={_config.ClientId}&" +
                             $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                             $"scope={Uri.EscapeDataString(scopes)}&" +
                             $"response_type=code&" +
                             $"state={state}&" +
                             $"access_type=offline&" +
                             $"prompt=consent";

                _logger.LogInformation("Generated auth URL successfully. RedirectUri: {RedirectUri}", redirectUri);

                var response = new
                {
                    AuthUrl = authUrl,
                    State = state,
                    RedirectUri = redirectUri,
                    CompanyAccount = _config.CompanyAccountEmail,
                    Instructions = "Company account (s3rcc.9@gmail.com) must authorize this application using the provided URL"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating company auth URL");
                return StatusCode(500, new { Error = "Error generating authorization URL", Details = ex.Message });
            }
        }

        /// <summary>
        /// Handle OAuth callback for company account authorization
        /// This stores the company account tokens for future use
        /// </summary>
        /// <param name="code">Authorization code from Google</param>
        /// <param name="state">State parameter for security</param>
        /// <returns>Setup completion status</returns>
        [HttpGet(ApiEndPointConstant.GoogleDrive.CompanyCallback)]
        public async Task<IActionResult> CompanyCallback([FromQuery] string code, [FromQuery] string state)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                {
                    return BadRequest("Authorization code is required");
                }

                _logger.LogInformation("Processing company account authorization callback");

                // Exchange code for tokens
                var tokens = await ExchangeCodeForTokensAsync(code);
                if (tokens == null)
                {
                    return BadRequest("Failed to exchange authorization code for tokens");
                }

                // Store company tokens
                await _oauthService.StoreCompanyTokensAsync(
                    tokens.AccessToken,
                    tokens.RefreshToken,
                    tokens.ExpiresAt
                );

                // Initialize company folder structure
                await _googleDriveService.InitializeCompanyFoldersAsync();

                _logger.LogInformation("Company account setup completed successfully");

                return Ok(new
                {
                    Success = true,
                    Message = "Company account authorized and folder structure initialized successfully",
                    CompanyAccount = _config.CompanyAccountEmail,
                    SetupCompletedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing company callback");
                return StatusCode(500, "Error completing company account setup");
            }
        }

        /// <summary>
        /// Check the current setup status
        /// </summary>
        /// <returns>Setup status information</returns>
        [HttpGet(ApiEndPointConstant.GoogleDrive.Status)]
        public async Task<IActionResult> GetSetupStatus()
        {
            try
            {
                var hasCompanyTokens = await _oauthService.HasValidCompanyTokensAsync();

                return Ok(new
                {
                    IsSetupComplete = hasCompanyTokens,
                    CompanyAccount = _config.CompanyAccountEmail,
                    HasValidTokens = hasCompanyTokens,
                    NextSteps = hasCompanyTokens
                        ? "Setup is complete. You can now use Google Drive for document storage."
                        : "Company account needs to be authorized. Use /company-auth-url to get authorization URL."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking setup status");
                return StatusCode(500, "Error checking setup status");
            }
        }

        /// <summary>
        /// Test Google Drive connection and permissions
        /// </summary>
        /// <returns>Connection test results</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrive.TestConnection)]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var hasTokens = await _oauthService.HasValidCompanyTokensAsync();
                if (!hasTokens)
                {
                    return BadRequest("Company account not authorized. Complete setup first.");
                }

                // Try to create a test folder
                await _googleDriveService.InitializeCompanyFoldersAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Google Drive connection successful",
                    CompanyAccount = _config.CompanyAccountEmail,
                    TestedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Drive connection test failed");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Google Drive connection test failed",
                    Error = ex.Message
                });
            }
        }

        #region Private Helper Methods

        private async Task<CompanyTokenResponse> ExchangeCodeForTokensAsync(string code)
        {
            try
            {
                var tokenRequest = new Dictionary<string, string>
                {
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = $"{_config.BaseUrl}/api/googledrive-setup/company-callback"
                };

                using var httpClient = new HttpClient();
                var content = new FormUrlEncodedContent(tokenRequest);
                var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token exchange failed: {Error}", errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                return new CompanyTokenResponse
                {
                    AccessToken = tokenResponse.GetProperty("access_token").GetString(),
                    RefreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.GetProperty("expires_in").GetInt32())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for tokens");
                return null;
            }
        }

        #endregion

        #region Data Models

        private class CompanyTokenResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        #endregion
    }
}
