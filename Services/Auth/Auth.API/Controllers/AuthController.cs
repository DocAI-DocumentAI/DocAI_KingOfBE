using System;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata.Ecma335;
using System.Security.Authentication;
using System.Threading.Tasks;
using Auth.API.Attributes;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.Auth;
using Auth.API.Payload.Request.GG;
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
using ForgotPasswordRequest = Auth.API.Payload.Request.User.ForgotPasswordRequest;
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
    /// <summary>
    /// Login
    /// </summary>
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
    /// <summary>
    /// Tạo tài khoản của user
    /// </summary>
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


    /// <summary>
    /// Láy mã OTP
    /// </summary>
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
    /// <summary>
    /// Thay đổi Roel cho user
    /// </summary>
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
    /// <summary>
    /// Thay đổi Department cho user
    /// </summary>
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


    /// <summary>
    /// Lấy toàn bộ user có filter
    /// </summary>    
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

    /// <summary>
    /// Logout user
    /// </summary>
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

    /// <summary>
    /// Login GG with GG token
    /// </summary>
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

    /// <summary>
    /// Login GG callback
    /// </summary>
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

    /// <summary>
    /// Lấy thông tin của user từ code login 
    /// </summary>
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

    /// <summary>
    /// Lấy URL để tiến hành login GG
    /// </summary>
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

    /// <summary>
    /// Lấy Refresh token của GG
    /// </summary>
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

    /// <summary>
    /// Dổi mật khẩu cho lần đầu login thường
    /// </summary>
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

    /// <summary>
    /// Cập nhật thoogn tin của user
    /// </summary>
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

    /// <summary>
    /// Cập nhật thông tin của chính mình
    /// </summary>
    [HttpPatch(ApiEndPointConstant.User.UpdateProfile)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateProfileRequest request)
    {
        var response = await _userService.UpdateUserProfileAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Cập nhật setting của mình
    /// </summary>
    [HttpPatch(ApiEndPointConstant.User.UpdateSettings)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(UserSettingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingRequest request)
    {
        var response = await _userService.UpdateUserSettingAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Lấy user bằng id
    /// </summary>
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

    /// <summary>
    /// Lấy danh sách user trong phòng ban của manager (lấy departmentId từ token)
    /// </summary>
    /// <param name="page">Số trang (mặc định: 1)</param>
    /// <param name="size">Kích thước trang (mặc định: 30)</param>
    /// <param name="keyword">Từ khóa tìm kiếm trong tên và email</param>
    /// <param name="sortBy">Trường sắp xếp</param>
    /// <param name="isAsc">Sắp xếp tăng dần (true) hoặc giảm dần (false)</param>
    [HttpGet(ApiEndPointConstant.User.GetDepartmentUsers)]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    [SkipRateLimit]
    [ProducesResponseType(typeof(IPaginate<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDepartmentUsersAsync(int page = 1, int size = 30, string? keyword = null, string? sortBy = null, bool isAsc = true)
    {
        try
        {
            var response = await _userService.GetDepartmentUsersAsync(page, size, keyword, sortBy, isAsc);
            return Ok(response);
        }
        catch (AuthenticationException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department users");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error occurred");
        }
    }

    /// <summary>
    /// Reset mật khẩu cho user bằng email
    /// </summary>
    [HttpPost(ApiEndPointConstant.User.ResetPassword)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPasswordByEmail([FromBody] ResetPasswordByEmailRequest request)
    {
        try
        {
            var result = await _userService.ResetPasswordByEmailAsync(request);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to reset password for email {request.Email}: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError($"User not found with email {request.Email}: {ex.Message}");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resetting password for email {request.Email}: {ex.Message}");
            return Problem(ex.Message);
        }
    }


    /// <summary>
    /// Xác thực mã OTP
    /// </summary>
    [HttpPost(ApiEndPointConstant.User.ValidateOtp)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateOtp([FromBody] CheckOtpRequest request)
    {
        try
        {
            var result = await _userService.ValidateOtpAsync(request);
            if (!result)
            {
                _logger.LogError($"OTP validation failed for email {request.Email}");
                return BadRequest("Mã OTP không chính xác hoặc đã hết hạn");
            }
        
            _logger.LogInformation($"OTP validation succeeded for email {request.Email}");
            return Ok(new { 
                success = result,
                message = "Mã OTP hợp lệ",
                email = request.Email
            });
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to validate OTP for email {request.Email}: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error validating OTP for email {request.Email}: {ex.Message}");
            return Problem(ex.Message);
        }
    }

    /// <summary>
    /// Quên mật khẩu - Reset mật khẩu bằng OTP
    /// </summary>
    [HttpPost(ApiEndPointConstant.User.ForgotPassword)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var result = await _userService.ForgotPasswordAsync(request);
            if (!result)
            {
                _logger.LogError($"Forgot password failed for email {request.Email}");
                return BadRequest("Không thể đặt lại mật khẩu");
            }
            
            _logger.LogInformation($"Password reset successfully for email {request.Email}");
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to reset password for email {request.Email}: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError($"User not found with email {request.Email}: {ex.Message}");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resetting password for email {request.Email}: {ex.Message}");
            return Problem(ex.Message);
        }
    }

}
