using System.Security.Claims;
using AutoMapper;
using ChatBox.API.Attributes;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ChatBox.API.Controllers
{
    /// <summary>
    /// API quản trị cấu hình AI và thống kê hệ thống
    /// </summary>
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("User ID not found in token");
        }
        /// <summary>
        /// Thống kê hoạt động theo ngày
        /// </summary>
        [HttpGet(ApiEndPointConstant.Admin.DailyActivity)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(List<DailyActivityResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDailyActivityAsync([FromQuery] int days = 30)
        {
            try
            {
                var response = await _adminService.GetDailyActivityAsync(days);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get daily activity");
                return Problem(MessageConstant.Admin.GetActivityFailed);
            }
        }

        /// <summary>
        /// Thống kê sử dụng từng model chi tiết
        /// </summary>
        [HttpGet(ApiEndPointConstant.Admin.ModelUsage)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(List<ModelUsageStatistics>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetModelUsageStatisticsAsync()
        {
            try
            {
                var response = await _adminService.GetModelUsageStatisticsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model usage statistics");
                return Problem(MessageConstant.Admin.GetModelUsageFailed);
            }
        }

        /// <summary>
        /// Bulk quản lý nhiều models (Optional - Advanced Feature)
        /// </summary>
        [HttpPost(ApiEndPointConstant.Admin.Bulk)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetMultipleActiveModelsAsync([FromBody] SetMultipleModelsRequest request)
        {
            try
            {
                var userId = GetUserId();
                var result = await _adminService.SetMultipleActiveModelsAsync(request.ModelNames, userId);

                if (!result)
                {
                    return BadRequest(MessageConstant.Admin.UpdateFailed);
                }

                _logger.LogInformation("Multiple models updated by {UserId}: {Models}",
                    userId, string.Join(", ", request.ModelNames));

                return Ok($"Đã cập nhật {request.ModelNames.Count} models thành công");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid bulk model request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set multiple active models");
                return Problem(MessageConstant.Admin.UpdateFailed);
            }
        }

        /// <summary>
        /// Phân tích ảnh hưởng trước khi tắt model (Optional - Advanced Feature)
        /// </summary>
        [HttpGet(ApiEndPointConstant.Admin.ModelImpact)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ModelImpactResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetModelImpactAnalysisAsync(string modelName)
        {
            try
            {
                var impact = await _adminService.GetModelImpactAnalysisAsync(modelName);
                return Ok(impact);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model impact analysis for {ModelName}", modelName);
                return Problem(MessageConstant.System.SystemError);
            }
        }
        /// <summary>
        /// Lấy danh sách tất cả cấu hình AI
        /// </summary>
        [HttpGet(ApiEndPointConstant.Admin.GetConfigurations)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(List<AIConfigurationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAIConfigurationsAsync()
        {
            try
            {
                var response = await _adminService.GetAIConfigurationsAsync(); 
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AI configurations");
                return Problem(MessageConstant.System.SystemError);
            }
        }
        /// <summary>
        /// Tạo cấu hình AI mới - validate và test connectivity
        /// </summary>
        [HttpPost(ApiEndPointConstant.Admin.CreateConfiguration)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(AIConfigurationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateAIConfigurationAsync([FromBody] AIConfigurationRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _adminService.CreateAIConfigurationAsync(request, userId);

                _logger.LogInformation("AI configuration created: {ModelName} by {UserId}",
                    request.ModelName, userId);

                return Created($"{ApiEndPointConstant.Admin.UpdateConfiguration.Replace("{configId}", response.Id)}", response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid AI configuration request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create AI configuration");
                return Problem(MessageConstant.Admin.CreateFailed);
            }
        }
        /// <summary>
        /// Cập nhật cấu hình AI - chỉ update các field được gửi
        /// </summary>
        [HttpPatch(ApiEndPointConstant.Admin.UpdateConfiguration)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(AIConfigurationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateAIConfigurationAsync(string configId, [FromBody] AIConfigurationRequest request)
        {
            try
            {
                var userId = GetUserId();
                var response = await _adminService.UpdateAIConfigurationAsync(configId, request, userId);

                if (response == null)
                {
                    return NotFound(MessageConstant.Admin.ConfigNotFound);
                }

                _logger.LogInformation("AI configuration updated: {ConfigId} by {UserId}", configId, userId);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid update request for config {ConfigId}: {Error}", configId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update AI configuration {ConfigId}", configId);
                return Problem(MessageConstant.Admin.UpdateFailed);
            }
        }

        /// <summary>
        /// Xóa cấu hình AI - tự động migrate sessions
        /// </summary>
        [HttpDelete(ApiEndPointConstant.Admin.DeleteConfiguration)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteAIConfigurationAsync(string configId)
        {
            try
            {
                var result = await _adminService.DeleteAIConfigurationAsync(configId);

                if (!result)
                    return NotFound(new { error = "Model không tồn tại" });

                _logger.LogInformation("AI configuration deleted with auto-migration: {ConfigId}", configId);

                return Ok(new { message = "Model đã được xóa thành công" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Cannot delete configuration {ConfigId}: {Error}", configId, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete AI configuration {ConfigId}", configId);
                return Problem("Failed to delete configuration");
            }
        }
        /// <summary>
        /// Kích hoạt model - chỉ 1 model active tại một thời điểm
        /// </summary>
        [HttpPost(ApiEndPointConstant.Admin.SetActiveModel)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ModelActivationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetActiveModelAsync(string configId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _adminService.TestAndActivateModelByIdAsync(configId, userId);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test and activate model");
                return Problem(MessageConstant.Admin.ActivateModelFailed);
            }
        }
        [HttpPost(ApiEndPointConstant.Admin.DeactivateModel)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateModelAsync(string configId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _adminService.DeactivateModelByIdAsync(configId, userId);

                if (!result.Success)
                    return NotFound(result.Message);

                return Ok(result.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("model cuối cùng"))
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate model {ConfigId}", configId);
                return Problem(MessageConstant.Admin.UpdateFailed);
            }
        }
        [HttpPost(ApiEndPointConstant.Admin.SetDefaultModel)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetDefaultModelAsync(string configId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _adminService.SetDefaultModelByIdAsync(configId, userId);

                if (!result.Success)
                    return BadRequest(result.Message);

                return Ok(result.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set default model {ConfigId}", configId);
                return Problem(MessageConstant.Admin.UpdateFailed);
            }
        }
        [HttpPost(ApiEndPointConstant.Admin.TestModel)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ModelTestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> TestModelAsync(string configId)
        {
            try
            {
                var userId = GetUserId();
                var response = await _adminService.TestModelByIdAsync(configId, userId);

                _logger.LogInformation("Model test completed: {ConfigId}, Success: {Success}",
                    configId, response.Success);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test model {ConfigId}", configId);
                return Problem(MessageConstant.Admin.TestModelFailed);
            }
        }

        /// <summary>
        /// Thống kê tổng quan hệ thống
        /// </summary>
        [HttpGet(ApiEndPointConstant.Admin.Statistics)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(SystemStatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSystemStatisticsAsync()
        {
            try
            {
                var response = await _adminService.GetSystemStatisticsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system statistics");
                return Problem(MessageConstant.Admin.GetStatsFailed);
            }
        }
    }
}