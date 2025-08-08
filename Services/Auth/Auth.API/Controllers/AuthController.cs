using System;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Auth.API.Attributes;
using Auth.API.Constants;
using Auth.API.DTOs.Request;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.Auth;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Auth;
using Auth.API.Payload.Response.User;
using Auth.API.Payload.Response.UserSetting;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using AutoMapper.Features;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class AuthController : ControllerBase
{
    private IUserService _userService;
    private IRedisService _redisService;
    private IGoogleOAuthService _googleOAuthService;
    private IConfiguration _configuration;
    readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger, IUserService userService, IRedisService redisService, IGoogleOAuthService googleOAuthService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _googleOAuthService = googleOAuthService ?? throw new ArgumentNullException(nameof(googleOAuthService));
    }

    [HttpPost(ApiEndPointConstant.User.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _userService.LoginAsync(request);
        if (response == null)
        {
            _logger.LogError($"Login failed with {request.Email}");
            return Problem(MessageConstant.User.LoginFailed);
        }
        _logger.LogInformation($"Login succeeded with {request.Email}");
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.User.CreateUser)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _userService.CreateUserAsync(request);
        if (response == null)
        {
            _logger.LogError($"Register failed with {request.Email}");
            return Problem(MessageConstant.User.RegisterFail);
        }
        _logger.LogInformation($"Register successful with {request.Email}");
        return CreatedAtAction(nameof(Register), response);
    }



    [HttpPost(ApiEndPointConstant.User.SendOtp)]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendOtp([FromBody] GenerateEmailOtpRequest request)
    {
        var result = await _userService.GenerateOtpAsync(request);
        if (result == null)
        {
            return Problem(MessageConstant.OTP.SendOtpFailed);
        }

        return CreatedAtAction(nameof(SendOtp), result);
    }

    [HttpPatch(ApiEndPointConstant.User.ChangeRole)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(UserRoleChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangeUserRole(Guid roleId)
    {
        try
        {
            var result = await _userService.ChangeUserRoleAsync(roleId);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to change user role: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError($"Unauthorized access when changing role: {ex.Message}");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error changing user role: {ex.Message}");
            return Problem(ex.Message);
        }
    }

    [HttpPatch(ApiEndPointConstant.User.ChangeDepartment)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(ChangeDepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangeDepartmentForUser([FromBody] ChangeDepartmentRequest request)
    {
        try
        {
            var result = await _userService.ChangeDepartmentForUserAsync(request);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to change user department: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError($"Unauthorized access when changing department: {ex.Message}");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error changing user department: {ex.Message}");
            return Problem(ex.Message);
        }
    }

    // [HttpPost(ApiEndPointConstant.User.GetUsersByDepartmentAndRole)]
    // [CustomAuthorize(Roles = new[] { Roles.Admin })]
    // [ProducesResponseType(typeof(List<GetUserByDeparAndRoleResponse>), StatusCodes.Status200OK)]
    // [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    // [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    // [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    // public async Task<IActionResult> GetUsersByDepartmentAndRole([FromBody] GetUserByDeparAndRole request)
    // {
    //     try
    //     {
    //         var result = await _userService.GetUserByDeparAndRoleAsync(request);
    //         return Ok(result);
    //     }
    //     catch (BadHttpRequestException ex)
    //     {
    //         _logger.LogError($"Failed to get users by department and role: {ex.Message}");
    //         return BadRequest(ex.Message);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError($"Error getting users by department and role: {ex.Message}");
    //         return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
    //     }
    // }

    [HttpGet(ApiEndPointConstant.User.Users)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [SkipRateLimit]
    [ProducesResponseType(typeof(IPaginate<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllUsersAsync(int page = 1, int size = 30,
        [FromQuery] UserFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        try
        {
            var response = await _userService.GetAllUsersAsync(page, size, filter, sortBy, isAsc);
            return Ok(response);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to get users: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting users: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPost(ApiEndPointConstant.User.Logout)]
    [CustomAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("userId")?.Value;
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            // Clear all user tokens from Redis
            await _redisService.ClearAllUserTokensAsync(userId);
        }

        if (!string.IsNullOrEmpty(jti))
        {
            // Add current JWT to blacklist
            var exp = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (long.TryParse(exp, out var expUnix))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).DateTime;
                var ttl = expiry - DateTime.UtcNow;
                if (ttl > TimeSpan.Zero)
                {
                    await _redisService.BlacklistJwtAsync(jti, ttl);
                }
            }
        }

        _logger.LogInformation($"User {userId} logged out successfully");
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost(ApiEndPointConstant.User.GoogleLogin)]
    [ProducesResponseType(typeof(GoogleOAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GoogleOAuthResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var response = await _userService.GoogleLoginAsync(request);
        if (response == null)
        {
            _logger.LogError($"Google login failed with token: {request.GoogleToken}");
            return Problem("Google login failed");
        }

        _logger.LogInformation($"Google login succeeded with email: {response.Email}");
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.User.GoogleCallback)]
    public async Task<IActionResult> GoogleCallback(string code, string state = null)
{
    if (string.IsNullOrEmpty(code))
    {
        // Nếu không có code, chuyển hướng về trang lỗi của frontend
        var errorUrl = _configuration["FrontEnd:ErrorUrl"] ?? "https://docai.asia/error";
        return Redirect($"{errorUrl}?message=auth_code_missing");
    }

    try
    {
        // 1. Tạo request object để truyền vào service
        var googleRequest = new GoogleOAuthRequest { Code = code, State = state };

        // 2. GỌI HÀM SERVICE ĐÚNG:
        // Hàm này sẽ xử lý code từ Google, tạo one-time-code mới
        // và trả về một URL hoàn chỉnh để redirect về frontend.
        var redirectUrl = await _googleOAuthService.HandleGoogleCallbackAndGenerateRedirectUrlAsync(googleRequest);

        // 3. THỰC HIỆN CHUYỂN HƯỚNG:
        // Đây là bước quan trọng nhất. Trình duyệt sẽ nhận lệnh này và
        // tự động điều hướng đến URL của frontend.
        // URL sẽ có dạng: "https://docai.asia/auth/callback?code=..."
        return Redirect(redirectUrl);
    }
    catch (UnauthorizedAccessException ex)
    {
        // Xử lý lỗi nếu người dùng không được phép
        var frontendLoginUrl = _configuration["FrontEnd:LoginUrl"] ?? "https://docai.asia/login";
        return Redirect($"{frontendLoginUrl}?error={Uri.EscapeDataString(ex.Message)}");
    }
    catch (Exception ex)
    {
        // Xử lý các lỗi server khác
        _logger.LogError(ex, "An error occurred during Google OAuth callback processing.");
        var frontendErrorUrl = _configuration["FrontEnd:ErrorUrl"] ?? "https://docai.asia/error";
        return Redirect($"{frontendErrorUrl}?message=server_error");
    }
}
    
    [HttpPost("exchange-code")]
    public async Task<IActionResult> ExchangeCode([FromBody] ExchangeCodeRequest request)
    {
        try
        {
            var loginResponse = await _googleOAuthService.ExchangeCodeForLoginResponseAsync(request.Code);
            return Ok(loginResponse);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Log lỗi
            return StatusCode(500, new { message = "An error occurred while exchanging the code." });
        }
    }

    [HttpGet(ApiEndPointConstant.User.GoogleAuthUrl)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult GetGoogleAuthUrl(string state = null)
    {
        var authUrl = _googleOAuthService.GetGoogleAuthUrl(state);
        return Ok(new { authUrl });
    }

    [HttpPost(ApiEndPointConstant.User.RevokeGoogleToken)]
    [Authorize]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeGoogleToken()
    {
        var userId = User.FindFirst("userId")?.Value; // Sửa: "userId" thay vì "UserId"
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("Invalid user");
        }

        var result = await _googleOAuthService.RevokeGoogleTokenAsync(userId);
        return Ok(new { success = result });
    }

    [HttpPost(ApiEndPointConstant.User.GoogleRefreshToken)]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var response = await _userService.RefreshTokenAsync(request); // Sửa: truyền request thay vì request.RefreshToken
        if (response == null)
        {
            _logger.LogError("Token refresh failed");
            return Unauthorized("Invalid refresh token");
        }

        _logger.LogInformation("Token refresh succeeded");
        return Ok(response);
    }

    [HttpPatch(ApiEndPointConstant.User.ChangePassword)]
    [CustomAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _userService.ChangePasswordAsync(Guid.Parse(userId), request);
        if (!result)
        {
            _logger.LogError($"Change password failed for user {userId}");
            return BadRequest("Change password failed");
        }

        _logger.LogInformation($"Password changed successfully for user {userId}");
        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPatch(ApiEndPointConstant.User.AdminUpdateUser)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminUpdateUser(Guid userId, [FromBody] AdminUpdateUserRequest request)
    {
        var response = await _userService.AdminUpdateUserAsync(userId, request);
        return Ok(response);
    }

    [HttpPatch(ApiEndPointConstant.User.UpdateProfile)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateProfileRequest request)
    {
        var response = await _userService.UpdateUserProfileAsync(request);
        return Ok(response);
    }

    [HttpPatch(ApiEndPointConstant.User.UpdateSettings)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(UserSettingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingRequest request)
    {
        var response = await _userService.UpdateUserSettingAsync(request);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.User.GetUserById)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var response = await _userService.GetUserByIdAminAsync(userId);
        return Ok(response);
    }
}
