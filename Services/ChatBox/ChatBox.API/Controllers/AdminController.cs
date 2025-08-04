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

namespace ChatBox.API.Controllers
{
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
                return Problem("Lấy danh sách cấu hình AI thất bại");
            }
        }

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

        [HttpDelete(ApiEndPointConstant.Admin.DeleteConfiguration)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteAIConfigurationAsync(string configId)
        {
            try
            {
                var result = await _adminService.DeleteAIConfigurationAsync(configId);

                if (!result)
                {
                    return NotFound(MessageConstant.Admin.ConfigNotFound);
                }

                _logger.LogInformation("AI configuration deleted: {ConfigId}", configId);
                return Ok(MessageConstant.Admin.ConfigDeleted);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Cannot delete configuration {ConfigId}: {Error}", configId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete AI configuration {ConfigId}", configId);
                return Problem(MessageConstant.Admin.DeleteFailed);
            }
        }

        [HttpPost(ApiEndPointConstant.Admin.SetActiveModel)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SetActiveModelAsync(string modelName)
        {
            try
            {
                var userId = GetUserId();
                var result = await _adminService.SetActiveModelAsync(modelName, userId);

                if (!result)
                {
                    return NotFound($"Model '{modelName}' không tồn tại");
                }

                _logger.LogInformation("Model activated: {ModelName} by {UserId}", modelName, userId);
                return Ok(MessageConstant.Admin.ModelActivated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate model {ModelName}", modelName);
                return Problem("Kích hoạt model thất bại");
            }
        }

        //[HttpPost(ApiEndPointConstant.Admin.TestModel)]
        //[CustomAuthorize(Roles = new[] { Roles.Admin })]
        //[ProducesResponseType(typeof(ModelTestResponse), StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status404NotFound)]
        //[ProducesResponseType(StatusCodes.Status401Unauthorized)]
        //[ProducesResponseType(StatusCodes.Status403Forbidden)]
        //public async Task<IActionResult> TestModelAsync(string modelName)
        //{
        //    try
        //    {
        //        var userId = GetUserId();
        //        var response = await _adminService.TestModelAsync(modelName, userId);

        //        _logger.LogInformation("Model test completed: {ModelName}, Success: {Success}",
        //            modelName, response.Success);

        //        return Ok(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to test model {ModelName}", modelName);
        //        return Problem("Test model thất bại");
        //    }
        //}

        [HttpGet(ApiEndPointConstant.Admin.Statistics)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
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
                return Problem("Lấy thống kê hệ thống thất bại");
            }
        }

        [HttpGet(ApiEndPointConstant.Admin.DailyActivity)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(List<DailyActivityResponse>), StatusCodes.Status200OK)]
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
                return Problem("Lấy thống kê hoạt động thất bại");
            }
        }

        [HttpGet(ApiEndPointConstant.Admin.ModelUsage)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(List<ModelUsageStatistics>), StatusCodes.Status200OK)]
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
                return Problem("Lấy thống kê sử dụng model thất bại");
            }
        }
    }
}