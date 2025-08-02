using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationLogService _logService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;
    public NotificationController(
           INotificationLogService logService,
           INotificationService notificationService,
           ILogger<NotificationController> logger)
    {
        _logService = logService;
        _notificationService = notificationService;
        _logger = logger;
    }
    [Authorize(Roles = "Admin,Manager")]
    [HttpGet("logs")]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationLogs([FromQuery] NotificationRequest request)
    {
        var logs = await _logService.GetNotificationLogsAsync(request);
        return Ok(logs);
    }
    [AllowAnonymous]
    [HttpGet("dismiss-by-token")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/html")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DismissNotificationByToken([FromQuery] Guid token)
    {
        if (token == Guid.Empty)
        {
            return BadRequest("Invalid token format.");
        }

        _logger.LogInformation("Processing dismiss request with token: {Token}", token);
        var resultMessage = await _notificationService.DismissNotificationByTokenAsync(token);

        var htmlResponse = $"<html><head><title>Notification Status</title><style>body{{font-family: sans-serif; text-align: center; padding-top: 50px;}}</style></head><body><h1>Notification Status</h1><p>{resultMessage}</p></body></html>";
        return Content(htmlResponse, "text/html");
    }
    [Authorize(Roles = "Admin,Manager")]
    [HttpPatch("logs/{logId:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DismissNotificationByUser(Guid logId)
    {
        // Lấy ID của người dùng đang thực hiện hành động từ JWT token
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Invalid user identifier in token.");
        }

        _logger.LogInformation("User {UserId} attempting to dismiss notification log {LogId}", userId, logId);
        var success = await _notificationService.DismissNotificationByUserAsync(logId, userId);

        if (success)
        {
            return NoContent();
        }

        return NotFound($"Notification log with ID {logId} not found or has already been dismissed.");
    }
}