using System.Security.Claims;
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
        /// API quản lý cấu hình notification system - thresholds, cron jobs, scheduling modes
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

            private Guid GetUserId()
            {
            var userIdClaim = User.FindFirst("userId")?.Value ??
                             User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
                return userId;

            throw new UnauthorizedAccessException("User ID not found or invalid in claims");
        }

            /// <summary>
            /// Lấy cấu hình notification hiện tại
            /// </summary>
            [HttpGet(ApiEndpointConstant.Config.Get)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
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
                    return Problem(MessageConstant.Config.GetFailed);
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

                    // ✅ NEW: Validate cron expressions before updating
                    if (!IsValidCronExpression(request.ExpiredNotificationCron))
                    {
                        return BadRequest($"Invalid expired notification cron expression: {request.ExpiredNotificationCron}");
                    }

                    if (!IsValidCronExpression(request.NearExpiredNotificationCron))
                    {
                        return BadRequest($"Invalid near-expired notification cron expression: {request.NearExpiredNotificationCron}");
                    }

                    var config = await _configService.UpdateNotificationConfigAsync(request);

                    _logger.LogInformation("Notification configuration updated by {UserId}. " +
                        "Expired: '{ExpiredCron}', NearExpired: '{NearExpiredCron}', Mode: {Mode}",
                        userId, request.ExpiredNotificationCron, request.NearExpiredNotificationCron, request.NearExpiredMode);

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

            /// <summary>
            /// Lấy trạng thái scheduler và next run times
            /// </summary>
            [HttpGet(ApiEndpointConstant.Config.GetStatus)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> GetStatusAsync()
            {
                try
                {
                    var status = await _configService.GetConfigWithStatusAsync();
                    return Ok(status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting notification config status");
                    return Problem("Failed to get notification status");
                }
            }

            /// <summary>
            /// Lấy thời gian chạy tiếp theo của các jobs
            /// </summary>
            [HttpGet(ApiEndpointConstant.Config.GetNextRuns)]
            [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> GetNextRunsAsync()
            {
                try
                {
                    var config = await _configService.GetNotificationConfigAsync();

                    return Ok(new
                    {
                        nextExpiredNotificationTime = config.NextExpiredNotificationTime,
                        nextNearExpiredNotificationTime = config.NextNearExpiredNotificationTime,
                        expiredNotificationCron = config.ExpiredNotificationCron,
                        nearExpiredNotificationCron = config.NearExpiredNotificationCron,
                        nearExpiredMode = config.NearExpiredMode,
                        nearExpiredModeDescription = config.NearExpiredModeDescription,
                        enableExpiredNotifications = config.EnableExpiredNotifications,
                        enableNearExpiredNotifications = config.EnableNearExpiredNotifications,
                        quartzEnabled = config.QuartzEnabled
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting next run times");
                    return Problem("Failed to get next run times");
                }
            }

            /// <summary>
            /// Trigger expired document notification job ngay lập tức - chỉ Admin
            /// </summary>
            [HttpPost(ApiEndpointConstant.Config.TriggerExpired)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status500InternalServerError)]
            public async Task<IActionResult> TriggerExpiredJobAsync()
            {
                try
                {
                    var userId = GetUserId();
                    await _schedulerService.TriggerExpiredDocumentJobNow();

                    _logger.LogInformation("Expired document job manually triggered by {UserId}", userId);
                    return Ok(new { message = "Expired document notification job triggered successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering expired document job");
                    return Problem(MessageConstant.Config.JobTriggerFailed);
                }
            }

            /// <summary>
            /// Trigger near-expired document notification job ngay lập tức - chỉ Admin
            /// </summary>
            [HttpPost(ApiEndpointConstant.Config.TriggerNearExpired)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status500InternalServerError)]
            public async Task<IActionResult> TriggerNearExpiredJobAsync()
            {
                try
                {
                    var userId = GetUserId();
                    await _schedulerService.TriggerNearExpiredDocumentJobNow();

                    _logger.LogInformation("Near-expired document job manually triggered by {UserId}", userId);
                    return Ok(new { message = "Near-expired document notification job triggered successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering near-expired document job");
                    return Problem(MessageConstant.Config.JobTriggerFailed);
                }
            }

            /// <summary>
            /// Trigger cleanup job ngay lập tức - chỉ Admin
            /// </summary>
            [HttpPost(ApiEndpointConstant.Config.TriggerCleanup)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status500InternalServerError)]
            public async Task<IActionResult> TriggerCleanupJobAsync()
            {
                try
                {
                    var userId = GetUserId();
                    await _schedulerService.TriggerCleanupJobNow();

                    _logger.LogInformation("Cleanup job manually triggered by {UserId}", userId);
                    return Ok(new { message = "Cleanup job triggered successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering cleanup job");
                    return Problem(MessageConstant.Config.JobTriggerFailed);
                }
            }

            /// <summary>
            /// Tạm dừng tất cả notification jobs - chỉ Admin
            /// </summary>
            [HttpPost(ApiEndpointConstant.Config.PauseJobs)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status500InternalServerError)]
            public async Task<IActionResult> PauseJobsAsync()
            {
                try
                {
                    var userId = GetUserId();
                    await _schedulerService.PauseAllJobs();

                    _logger.LogInformation("All notification jobs paused by {UserId}", userId);
                    return Ok(new { message = MessageConstant.Config.JobsPaused });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pausing notification jobs");
                    return Problem(MessageConstant.Config.JobControlFailed);
                }
            }

            /// <summary>
            /// Tiếp tục tất cả notification jobs - chỉ Admin
            /// </summary>
            [HttpPost(ApiEndpointConstant.Config.ResumeJobs)]
            [CustomAuthorize(Roles = new[] { Roles.Admin })]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status500InternalServerError)]
            public async Task<IActionResult> ResumeJobsAsync()
            {
                try
                {
                    var userId = GetUserId();
                    await _schedulerService.ResumeAllJobs();

                    _logger.LogInformation("All notification jobs resumed by {UserId}", userId);
                    return Ok(new { message = MessageConstant.Config.JobsResumed });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming notification jobs");
                    return Problem(MessageConstant.Config.JobControlFailed);
                }
            }

            private static bool IsValidCronExpression(string cronExpression)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(cronExpression))
                        return false;

                    // Simple validation - can be enhanced with Quartz CronExpression.IsValidExpression
                    var parts = cronExpression.Split(' ');
                    return parts.Length == 6; // Basic cron format: second minute hour day month dayOfWeek
                }
                catch
                {
                    return false;
                }
            }
        }
}