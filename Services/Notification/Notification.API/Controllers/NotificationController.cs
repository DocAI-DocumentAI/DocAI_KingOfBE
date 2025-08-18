using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Notification.Api.Constants;
using Notification.API.Attributes;
using Notification.API.Constants;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Controllers;

/// <summary>
/// API quản lý thông báo - xem logs, dismiss notifications
/// </summary>
[ApiController]
[Route(ApiEndpointConstant.ApiEndpoint)]
public class NotificationController : ControllerBase
{
    private readonly INotificationLogService _logService;
    private readonly INotificationService _notificationService;
    private readonly IAuthorizationService _authService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationLogService logService,
        INotificationService notificationService,
        IAuthorizationService authService,
        ILogger<NotificationController> logger)
    {
        _logService = logService;
        _notificationService = notificationService;
        _authService = authService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst("userId")?.Value ??
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// Lấy danh sách notification logs -  user khác chỉ xem của mình
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetLogs)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotificationLogsAsync([FromQuery] NotificationRequest request)
    {
        try
        {
            var currentUserEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(currentUserEmail))
            {
                request.Recipient = currentUserEmail; // Force filter theo email của user hiện tại
            }
            else
            {
                _logger.LogWarning("User email not found in token");
                return BadRequest("User email not found in token");
            }


            var logs = await _logService.GetNotificationLogsAsync(request);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notification logs");
            return Problem("Failed to retrieve notification logs");
        }
    }
    // ✅ HOẶC nếu muốn Admin có API riêng để xem tất cả (tùy chọn)
    /// <summary>
    /// [ADMIN ONLY] Lấy tất cả notification logs của hệ thống
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetAllSystemLogs)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllSystemLogsAsync([FromQuery] NotificationRequest request)
    {
        try
        {
            // Admin có thể xem tất cả mà không filter
            var logs = await _logService.GetNotificationLogsAsync(request);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system notification logs");
            return Problem("Failed to retrieve system notification logs");
        }
    }
}