using System.Security.Claims;
using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        // AI Configuration endpoints
        [HttpGet("ai-configurations")]
        public async Task<ActionResult<ApiResponse<List<AIConfigurationResponse>>>> GetAIConfigurations()
        {
            try
            {
                var configs = await _adminService.GetAIConfigurationsAsync();
                return Ok(ApiResponse<List<AIConfigurationResponse>>.Ok(configs));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<AIConfigurationResponse>>.Fail("Đã xảy ra lỗi khi lấy cấu hình AI."));
            }
        }

        [HttpPost("ai-configurations")]
        public async Task<ActionResult<ApiResponse<AIConfigurationResponse>>> CreateAIConfiguration([FromBody] AIConfigurationRequest request)
        {
            try
           { 
                var userId = GetCurrentUserId();
                var response = await _adminService.CreateAIConfigurationAsync(request, userId);
                return Ok(ApiResponse<AIConfigurationResponse>.Ok(response, "Tạo cấu hình AI thành công."));
        }
            catch (Exception ex)
           {
              return StatusCode(500, ApiResponse<AIConfigurationResponse>.Fail("Đã xảy ra lỗi khi tạo cấu hình AI."));
          }
}

        [HttpPut("ai-configurations/{id}")]
        public async Task<ActionResult<ApiResponse<AIConfigurationResponse>>> UpdateAIConfiguration(
            string id,
            [FromBody] AIConfigurationRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _adminService.UpdateAIConfigurationAsync(id, request, userId);
                return Ok(ApiResponse<AIConfigurationResponse>.Ok(response, "Cập nhật cấu hình AI thành công."));
            }
            catch (ArgumentException ex)
            {
                return NotFound(ApiResponse<AIConfigurationResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AIConfigurationResponse>.Fail("Đã xảy ra lỗi khi cập nhật cấu hình AI."));
            }
        }

        [HttpDelete("ai-configurations/{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAIConfiguration(string id)
        {
            try
            {
                var result = await _adminService.DeleteAIConfigurationAsync(id);
                if (result)
                    return Ok(ApiResponse<bool>.Ok(true, "Xóa cấu hình AI thành công."));
                else
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy cấu hình AI."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi xóa cấu hình AI."));
            }
        }

        // Prohibited Words endpoints
        [HttpGet("prohibited-words")]
        public async Task<ActionResult<ApiResponse<List<ProhibitedWordResponse>>>> GetProhibitedWords()
        {
            try
            {
                var words = await _adminService.GetProhibitedWordsAsync();
                return Ok(ApiResponse<List<ProhibitedWordResponse>>.Ok(words));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<ProhibitedWordResponse>>.Fail("Đã xảy ra lỗi khi lấy danh sách từ cấm."));
            }
        }

        [HttpPost("prohibited-words")]
        public async Task<ActionResult<ApiResponse<ProhibitedWordResponse>>> CreateProhibitedWord([FromBody] ProhibitedWordRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var response = await _adminService.CreateProhibitedWordAsync(request, userId);
                return Ok(ApiResponse<ProhibitedWordResponse>.Ok(response, "Thêm từ cấm thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ProhibitedWordResponse>.Fail("Đã xảy ra lỗi khi thêm từ cấm."));
            }
        }

        [HttpDelete("prohibited-words/{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProhibitedWord(string id)
        {
            try
            {
                var result = await _adminService.DeleteProhibitedWordAsync(id);
                if (result)
                    return Ok(ApiResponse<bool>.Ok(true, "Xóa từ cấm thành công."));
                else
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy từ cấm."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Đã xảy ra lỗi khi xóa từ cấm."));
            }
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("Không thể xác định người dùng.");
        }
    }
}