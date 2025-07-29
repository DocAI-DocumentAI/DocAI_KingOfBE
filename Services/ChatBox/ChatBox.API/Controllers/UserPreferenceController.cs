using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
    public class UserPreferenceController : ControllerBase
    {
        private readonly IUserPreferenceService _userPreferenceService;

        public UserPreferenceController(IUserPreferenceService userPreferenceService)
        {
            _userPreferenceService = userPreferenceService;
        }

        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> GetUserPreferences(string sessionId)
        {
            try
            {
                var preferences = await _userPreferenceService.GetUserPreferencesAsync(sessionId);
                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi lấy tùy chọn người dùng."));
            }
        }

        [HttpPut("sessions/{sessionId}")]
        public async Task<ActionResult<ApiResponse<UserPreferenceResponse>>> UpdateUserPreferences(
            string sessionId,
            [FromBody] UserPreferenceRequest request)
        {
            try
            {
                var preferences = await _userPreferenceService.UpdateUserPreferencesAsync(sessionId, request);
                return Ok(ApiResponse<UserPreferenceResponse>.Ok(preferences, "Cập nhật tùy chọn thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<UserPreferenceResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserPreferenceResponse>.Fail("Đã xảy ra lỗi khi cập nhật tùy chọn."));
            }
        }

        [HttpGet("characteristics")]
        public async Task<ActionResult<ApiResponse<List<CharacteristicOption>>>> GetAvailableCharacteristics()
        {
            try
            {
                var characteristics = await _userPreferenceService.GetAvailableCharacteristicsAsync();
                return Ok(ApiResponse<List<CharacteristicOption>>.Ok(characteristics));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<CharacteristicOption>>.Fail("Đã xảy ra lỗi khi lấy danh sách đặc điểm."));
            }
        }
    }
}