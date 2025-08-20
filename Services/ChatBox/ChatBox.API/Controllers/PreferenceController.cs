using System.Text.Json;
using ChatBox.API.Attributes;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
    /// <summary>
    /// API quản lý tùy chọn user và session - Simplified Version
    /// </summary>
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    public class PreferenceController : ControllerBase
    {
        private readonly IPreferenceService _preferenceService;
        private readonly ILogger<PreferenceController> _logger;

        public PreferenceController(IPreferenceService preferenceService, ILogger<PreferenceController> logger)
        {
            _preferenceService = preferenceService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("User ID not found in token");
        }

        /// <summary>
        /// Lấy tùy chọn cá nhân của user (User Default - áp dụng cho tất cả session mới)
        /// </summary>
        [HttpGet(ApiEndPointConstant.Preference.GetUserPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUserChatPreferencesAsync()
        {
            try
            {
                var userId = GetUserId();
                var response = await _preferenceService.GetUserChatPreferencesAsync(userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user preferences");
                return Problem("Lấy tùy chọn người dùng thất bại");
            }
        }

        /// <summary>
        /// Cập nhật tùy chọn cá nhân (User Default - áp dụng cho tất cả session mới)
        /// </summary>
        [HttpPatch(ApiEndPointConstant.Preference.UpdateUserPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateUserChatPreferencesAsync([FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Request body không được để trống" });
                }

                var userId = GetUserId();
                var response = await _preferenceService.UpdateUserChatPreferencesAsync(userId, request);

                _logger.LogInformation("User preferences updated for {UserId}", userId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error updating user preferences");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user preferences");
                return Problem(MessageConstant.Preference.UpdateFailed);
            }
        }

        /// <summary>
        /// Lấy tùy chọn hiệu quả cho session (Session Override > User Default > Empty)
        /// </summary>
        [HttpGet(ApiEndPointConstant.Preference.GetSessionPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSessionPreferencesAsync(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _preferenceService.GetSessionPreferencesAsync(sessionId, userId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session preferences for {SessionId}", sessionId);
                return Problem("Lấy tùy chọn phiên chat thất bại");
            }
        }

        /// <summary>
        /// Cập nhật tùy chọn cho session cụ thể (Session Override - chỉ áp dụng cho session này)
        /// </summary>
        [HttpPatch(ApiEndPointConstant.Preference.UpdateSessionPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateSessionPreferencesAsync(string sessionId, [FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Request body không được để trống" });
                }

                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new { message = "SessionId không được để trống" });
                }

                var userId = GetUserId();
                var response = await _preferenceService.UpdateSessionPreferencesAsync(sessionId, userId, request);

                _logger.LogInformation("Session preferences updated for {SessionId} by {UserId}", sessionId, userId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error updating session preferences for {SessionId}", sessionId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session preferences for {SessionId}", sessionId);
                return Problem(MessageConstant.Preference.UpdateFailed);
            }
        }

        /// <summary>
        /// Xóa tùy chọn cá nhân của user (User Default)
        /// </summary>
        [HttpDelete(ApiEndPointConstant.Preference.DeleteUserPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteUserPreferencesAsync()
        {
            try
            {
                var userId = GetUserId();
                var result = await _preferenceService.DeleteUserPreferencesAsync(userId);

                if (!result)
                {
                    return NotFound(MessageConstant.Preference.PreferenceNotFound);
                }

                _logger.LogInformation("User preferences deleted for {UserId}", userId);
                return Ok(MessageConstant.Preference.PreferenceDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user preferences");
                return Problem(MessageConstant.Preference.DeleteFailed);
            }
        }

        /// <summary>
        /// Xóa tùy chọn riêng của session (Session Override - về lại User Default)
        /// </summary>
        [HttpDelete(ApiEndPointConstant.Preference.DeleteSessionPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteSessionPreferencesAsync(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _preferenceService.DeleteSessionPreferencesAsync(sessionId, userId);

                if (!result)
                {
                    return NotFound(MessageConstant.Preference.PreferenceNotFound);
                }

                _logger.LogInformation("Session preferences deleted for {SessionId} by {UserId}", sessionId, userId);
                return Ok(MessageConstant.Preference.PreferenceDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session preferences for {SessionId}", sessionId);
                return Problem(MessageConstant.Preference.DeleteFailed);
            }
        }

        /// <summary>
        /// Lấy danh sách characteristics có sẵn
        /// </summary>
        [HttpGet(ApiEndPointConstant.Preference.GetAvailableCharacteristics)]
        [ProducesResponseType(typeof(List<CharacteristicOption>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableCharacteristicsAsync()
        {
            try
            {
                var availableCharacteristics = ChatbotCharacteristics.Available.Select(c => new CharacteristicOption
                {
                    Value = c.Value,
                    DisplayName = c.DisplayName,
                    IsSelected = false // Default không chọn
                }).ToList();

                return Ok(availableCharacteristics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available characteristics");
                return Problem("Lấy danh sách đặc điểm thất bại");
            }
        }

        /// <summary>
        /// ✅ NEW: API để Frontend biết preference status cho UI indicators
        /// </summary>
        [HttpGet("preference/status/{sessionId}")]
        [CustomAuthorize]
        [ProducesResponseType(typeof(PreferenceStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPreferenceStatusAsync(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var status = await _preferenceService.GetPreferenceStatusAsync(sessionId, userId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get preference status for session {SessionId}", sessionId);
                return Problem("Lấy trạng thái preference thất bại");
            }
        }
    }
}