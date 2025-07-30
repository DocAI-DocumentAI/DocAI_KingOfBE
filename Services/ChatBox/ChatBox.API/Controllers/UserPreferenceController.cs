using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserPreferenceController : ControllerBase
    {
        private readonly IPreferenceService _preferenceService;

        public UserPreferenceController(IPreferenceService preferenceService)
        {
            _preferenceService = preferenceService;
        }

        [HttpGet("user")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> GetUserPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.GetUserChatPreferencesAsync(userId);
                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi lấy user preferences."));
            }
        }

        [HttpPut("user")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> UpdateUserPreferences(
            [FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.UpdateUserChatPreferencesAsync(userId, request);

                var message = request.ApplyToNewChats
                    ? "Cập nhật preferences thành công. Sẽ áp dụng cho các cuộc trò chuyện mới."
                    : "Cập nhật preferences thành công. Chỉ áp dụng cho session hiện tại.";

                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences, message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi cập nhật user preferences."));
            }
        }

        [HttpDelete("user")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUserPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _preferenceService.DeleteUserPreferencesAsync(userId);

                if (result)
                    return Ok(ApiResponse<bool>.Ok(true, "Xóa user preferences thành công."));
                else
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy user preferences."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi xóa user preferences."));
            }
        }

        // ✅ SESSION-LEVEL PREFERENCES

        [HttpGet("session/{sessionId}")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> GetSessionPreferences(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.GetSessionPreferencesAsync(sessionId, userId);
                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi lấy session preferences."));
            }
        }

        [HttpPut("session/{sessionId}")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> UpdateSessionPreferences(
            string sessionId,
            [FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.UpdateSessionPreferencesAsync(sessionId, userId, request);

                var message = request.ApplyToNewChats
                    ? "Cập nhật session preferences thành công và sẽ áp dụng cho các cuộc trò chuyện mới."
                    : "Cập nhật session preferences thành công cho session này.";

                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences, message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi cập nhật session preferences."));
            }
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteSessionPreferences(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _preferenceService.DeleteSessionPreferencesAsync(sessionId, userId);

                if (result)
                    return Ok(ApiResponse<bool>.Ok(true, "Xóa session preferences thành công."));
                else
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy session preferences."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi xóa session preferences."));
            }
        }

        // ✅ EFFECTIVE PREFERENCES (combined)

        [HttpGet("effective/{sessionId}")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> GetEffectivePreferences(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var preferences = await _preferenceService.GetEffectivePreferencesAsync(sessionId, userId);
                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi lấy effective preferences."));
            }
        }

        [HttpGet("has-user-preferences")]
        public async Task<ActionResult<ApiResponse<bool>>> HasUserPreferences()
        {
            try
            {
                var userId = GetCurrentUserId();
                var hasPreferences = await _preferenceService.HasUserPreferencesAsync(userId);
                return Ok(ApiResponse<bool>.Ok(hasPreferences));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi kiểm tra user preferences."));
            }
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        }
    }
}