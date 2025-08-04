using ChatBox.API.Attributes;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
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

        [HttpPatch(ApiEndPointConstant.Preference.UpdateUserPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateUserChatPreferencesAsync([FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _preferenceService.UpdateUserChatPreferencesAsync(userId, request);

                _logger.LogInformation("User preferences updated for {UserId}", userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user preferences");
                return Problem(MessageConstant.Preference.UpdateFailed);
            }
        }

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

        [HttpPatch(ApiEndPointConstant.Preference.UpdateSessionPreferences)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(UserPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateSessionPreferencesAsync(string sessionId, [FromBody] UpdatePreferenceRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _preferenceService.UpdateSessionPreferencesAsync(sessionId, userId, request);

                _logger.LogInformation("Session preferences updated for {SessionId} by {UserId}", sessionId, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session preferences for {SessionId}", sessionId);
                return Problem(MessageConstant.Preference.UpdateFailed);
            }
        }

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
    }
}