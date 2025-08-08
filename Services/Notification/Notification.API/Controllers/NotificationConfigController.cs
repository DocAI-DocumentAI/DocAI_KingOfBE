using Microsoft.AspNetCore.Mvc;
using Notification.Api.Constants;
using Notification.API.Attributes;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Controllers
{
    /// <summary>
    /// API quản lý cấu hình notification system - thresholds, cron jobs, etc.
    /// </summary>
    [ApiController]
    [Route(ApiEndpointConstant.ApiEndpoint)]
    public class NotificationConfigController : ControllerBase
    {
        private readonly INotificationConfigService _configService;
        private readonly INotificationSchedulerService _schedulerService;
        private readonly IAuthorizationService _authService;
        private readonly ILogger<NotificationConfigController> _logger;

        public NotificationConfigController(
            INotificationConfigService configService,
            INotificationSchedulerService schedulerService,
            IAuthorizationService authService,
            ILogger<NotificationConfigController> logger)
        {
            _configService = configService;
            _schedulerService = schedulerService;
            _authService = authService;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirst("userId")?.Value ??
                   throw new UnauthorizedAccessException("User ID not found in token");
        }

        /// <summary>
        /// Lấy cấu hình notification hiện tại
        /// </summary>
        [HttpGet(ApiEndpointConstant.Config.Get)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(NotificationConfigResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetConfigAsync()
        {
            try
            {
                var config = await _configService.GetNotificationConfigAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notification configuration");
                return Problem("Failed to retrieve notification configuration");
            }
        }

        /// <summary>
        /// Cập nhật cấu hình notification - chỉ Admin
        /// </summary>
        [HttpPut(ApiEndpointConstant.Config.Update)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(NotificationConfigResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateConfigAsync([FromBody] NotificationConfigRequest request)
        {
            try
            {
                var userId = GetUserId();
                var config = await _configService.UpdateNotificationConfigAsync(request);

                // Update scheduler if cron expression changed
                await _schedulerService.UpdateDocumentScanJobSchedule(request.ScanCronExpression);

                _logger.LogInformation("Notification configuration updated by {UserId}", userId);
                return Ok(config);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogWarning("Invalid notification config request: {Error}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update notification configuration");
                return Problem(MessageConstant.Config.UpdateFailed);
            }
        }
    }
}