using System.Text.Json;
using Auth.API.DTOs.Request;
using Auth.API.DTOs.Response;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.Role;
using Auth.API.Services.Interface;
using Auth.API.Utils;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement;

public class GoogleOAuthService : BaseService<GoogleOAuthService>, IGoogleOAuthService
{
    private readonly IRedisService _redisService;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public GoogleOAuthService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<GoogleOAuthService> logger,
            IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper,
            HttpClient httpClient,
            IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _redisService = redisService;
        _mapper = mapper;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;

        _clientId = configuration["GoogleOAuth:ClientId"] ?? throw new InvalidOperationException("GoogleOAuth:ClientId is missing");
        _clientSecret = configuration["GoogleOAuth:ClientSecret"] ?? throw new InvalidOperationException("GoogleOAuth:ClientSecret is missing");
        _redirectUri = configuration["GoogleOAuth:RedirectUri"] ?? throw new InvalidOperationException("GoogleOAuth:RedirectUri is missing");
    }

    public async Task<GoogleAuthUrlResponse> GetAuthUrlAsync()
    {
        var state = Guid.NewGuid().ToString();
        var scopes = "openid email profile";

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                     $"client_id={_clientId}&" +
                     $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                     $"scope={Uri.EscapeDataString(scopes)}&" +
                     $"response_type=code&" +
                     $"state={state}&" +
                     $"access_type=offline&" +
                     $"prompt=consent";

        return new GoogleAuthUrlResponse
        {
            AuthUrl = authUrl,
            State = state
        };
    }

    public async Task<GoogleOAuthResponse> AuthenticateWithGoogleAsync(GoogleOAuthRequest request)
    {
        try
        {
            var tokenResponse = await ExchangeCodeForTokensAsync(request.Code);
            if (tokenResponse == null)
            {
                throw new UnauthorizedAccessException("Failed to exchange code for tokens");
            }

            var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
            if (userInfo == null)
            {
                throw new UnauthorizedAccessException("Failed to get user info from Google");
            }

            var user = await FindOrCreateUserAsync(userInfo);

            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            await _redisService.SetGoogleTokensAsync(user.Id.ToString(),
                tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt);

            var accessToken = JwtUtil.GenerateJwtToken(user, _configuration);
            var refreshToken = JwtUtil.GenerateJwtRefreshToken(user, _configuration);

            await _redisService.SetDocAITokensAsync(user.Id.ToString(), accessToken, refreshToken);
            await UpdateLastLoginAsync(user);

            return CreateGoogleOAuthResponse(user, accessToken, refreshToken, tokenResponse.AccessToken, tokenResponse.RefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google OAuth authentication");
            throw;
        }
    }

    public async Task<LoginResponse> AuthenticateWithTokenAsync(GoogleTokenRequest request)
    {
        try
        {
            var userInfo = await GetGoogleUserInfoAsync(request.AccessToken);
            if (userInfo == null)
            {
                throw new UnauthorizedAccessException("Invalid Google access token");
            }

            var user = await FindOrCreateUserAsync(userInfo);

            var accessToken = JwtUtil.GenerateJwtToken(user, _configuration);
            var refreshToken = JwtUtil.GenerateJwtRefreshToken(user, _configuration);

            await _redisService.SetDocAITokensAsync(user.Id.ToString(), accessToken, refreshToken);
            await UpdateLastLoginAsync(user);

            return CreateLoginResponse(user, accessToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google token authentication");
            throw;
        }
    }

    public async Task<bool> RevokeGoogleTokenAsync(string userId)
    {
        try
        {
            var tokens = await _redisService.GetGoogleTokensAsync(userId);
            if (tokens.HasValue)
            {
                var revokeUrl = $"https://oauth2.googleapis.com/revoke?token={tokens.Value.accessToken}";
                var response = await _httpClient.PostAsync(revokeUrl, null);

                await _redisService.ClearGoogleTokensAsync(userId);
                return response.IsSuccessStatusCode;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Google token for user: {UserId}", userId);
            return false;
        }
    }

    private async Task<GoogleTokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        try
        {
            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = _redirectUri
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google token exchange failed: {Error}", errorContent);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GoogleTokenResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for tokens");
            return null;
        }
    }

    private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Google user info: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GoogleUserInfo>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Google user info");
            return null;
        }
    }

    private async Task<User> FindOrCreateUserAsync(GoogleUserInfo userInfo)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Email == userInfo.Email,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission));

        if (user == null)
        {
            // KHÔNG TẠO USER MỚI - chỉ admin mới được tạo
            throw new UnauthorizedAccessException($"No account found for email: {userInfo.Email}. Please contact administrator to create your account first.");
        }

        // Cập nhật GoogleId nếu chưa có
        if (string.IsNullOrEmpty(user.GoogleId))
        {
            user.GoogleId = userInfo.Id;
            user.UpdateAt = DateTime.UtcNow;
            _unitOfWork.GetRepository<User>().UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Linked Google account to existing user: {Email}", userInfo.Email);
        }

        return user;
    }

    private async Task<Role> GetDefaultRoleAsync()
    {
        return await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.RoleName == "Member")
            ?? throw new InvalidOperationException("Default role 'Member' not found");
    }

    private async Task<Department?> GetDefaultDepartmentAsync()
    {
        return await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Name == "General");
    }

    private async Task UpdateLastLoginAsync(User user)
    {
        user.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(user);
        await _unitOfWork.CommitAsync();
    }

    private LoginResponse CreateLoginResponse(User user, string accessToken, string refreshToken)
    {
        return new LoginResponse
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            Phone = user.Phone ?? "",
            Role = new RoleResponse
            {
                Id = user.Role?.Id ?? Guid.Empty,
                RoleName = user.Role?.RoleName ?? "",
                Description = user.Role?.Description ?? "",
                CreateAt = user.Role?.CreateAt ?? DateTime.UtcNow,
                UpdateAt = user.Role?.UpdateAt ?? DateTime.UtcNow
            },
            Department = new DepartmentResponse
            {
                Id = user.Department?.Id ?? Guid.Empty,
                Name = user.Department?.Name ?? "",
                Description = user.Department?.Description ?? "",
                CreateAt = user.Department?.CreateAt ?? DateTime.UtcNow,
                UpdateAt = user.Department?.UpdateAt ?? DateTime.UtcNow
            },
            DocaiToken = accessToken,
            DocaiRefreshToken = refreshToken,
            RequirePasswordChange = false
        };
    }

    public async Task<GoogleTokenValidationResult> ValidateGoogleTokenAsync(string googleToken)
    {
        try
        {
            var userInfo = await GetGoogleUserInfoAsync(googleToken);
            if (userInfo == null)
            {
                return new GoogleTokenValidationResult { IsValid = false };
            }

            return new GoogleTokenValidationResult
            {
                IsValid = true,
                Email = userInfo.Email,
                GoogleId = userInfo.Id,
                Name = userInfo.Name,
                Picture = userInfo.Picture,
                AccessToken = googleToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return new GoogleTokenValidationResult { IsValid = false };
        }
    }

    public async Task<(string accessToken, string refreshToken)?> RefreshGoogleTokenAsync(string refreshToken)
    {
        try
        {
            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google token refresh failed");
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return (tokenResponse.AccessToken, tokenResponse.RefreshToken ?? refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Google token");
            return null;
        }
    }

    public string GetGoogleAuthUrl(string state = null)
    {
        state ??= Guid.NewGuid().ToString();
        var scopes = "openid email profile";

        return $"https://accounts.google.com/o/oauth2/v2/auth?" +
               $"client_id={_clientId}&" +
               $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
               $"scope={Uri.EscapeDataString(scopes)}&" +
               $"response_type=code&" +
               $"state={state}&" +
               $"access_type=offline&" +
               $"prompt=consent";
    }

    private GoogleOAuthResponse CreateGoogleOAuthResponse(User user, string docaiToken, string docaiRefreshToken, string googleAccessToken, string googleRefreshToken)
    {
        return new GoogleOAuthResponse
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            Phone = user.Phone ?? "",
            Role = new RoleResponse
            {
                Id = user.Role?.Id ?? Guid.Empty,
                RoleName = user.Role?.RoleName ?? "",
                Description = user.Role?.Description ?? ""
            },
            Department = new DepartmentResponse
            {
                Id = user.Department?.Id ?? Guid.Empty,
                Name = user.Department?.Name ?? "",
                Description = user.Department?.Description ?? ""
            },
            DocaiToken = docaiToken,
            DocaiRefreshToken = docaiRefreshToken,
            GoogleAccessToken = googleAccessToken,
            GoogleRefreshToken = googleRefreshToken,
            RequirePasswordChange = false
        };
    }

}




