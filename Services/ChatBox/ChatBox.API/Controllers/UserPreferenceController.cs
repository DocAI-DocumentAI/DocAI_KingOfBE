using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.Domain.Models;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/users/preferences")]
    [Authorize]
    public class UserPreferenceController : ControllerBase
    {
        private readonly IUserPreferenceService _userPreferenceService;
        private readonly ILogger<UserPreferenceController> _logger;

        public UserPreferenceController(
            IUserPreferenceService userPreferenceService, 
            ILogger<UserPreferenceController> logger)
        {
            _userPreferenceService = userPreferenceService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        /// <summary>
        /// Get user preferences
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<UserPreferenceResponse>> GetPreferences()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var response = await _userPreferenceService.GetPreferenceResponseAsync(userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences for user {UserId}", GetUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get raw user preference entity
        /// </summary>
        [HttpGet("raw")]
        public async Task<ActionResult<UserPreference>> GetRawPreferences()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var preference = await _userPreferenceService.GetPreferenceAsync(userId);
                return Ok(preference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting raw user preferences for user {UserId}", GetUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update user preferences
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<bool>> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var result = await _userPreferenceService.UpdatePreferenceAsync(userId, request);
                
                if (result)
                {
                    return Ok(new { success = true, message = "Preferences updated successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to update preferences" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user preferences for user {UserId}", GetUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Reset user preferences to default
        /// </summary>
        [HttpPost("reset")]
        public async Task<ActionResult<bool>> ResetPreferences()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var result = await _userPreferenceService.ResetPreferencesAsync(userId);
                
                if (result)
                {
                    return Ok(new { success = true, message = "Preferences reset successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to reset preferences" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user preferences for user {UserId}", GetUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get default preferences (public endpoint)
        /// </summary>
        [HttpGet("defaults")]
        [AllowAnonymous]
        public async Task<ActionResult<UserPreferenceResponse>> GetDefaultPreferences()
        {
            try
            {
                var response = await _userPreferenceService.GetDefaultPreferencesAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default preferences");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Set default preferences (Admin only)
        /// </summary>
        [HttpPost("defaults")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> SetDefaultPreferences([FromBody] SetDefaultPreferencesRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _userPreferenceService.SetDefaultPreferencesAsync(request);
                
                if (result)
                {
                    return Ok(new { success = true, message = "Default preferences set successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to set default preferences" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default preferences");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
