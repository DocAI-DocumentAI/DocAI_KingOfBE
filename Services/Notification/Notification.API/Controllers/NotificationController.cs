using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Notification.Api.Constants;
using Notification.API.Attributes;
using Notification.API.Constants;
using Notification.API.Controllers;
using Notification.API.Hubs;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Implement;
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
    private readonly INotificationReadService _readService;
    private readonly IAuthorizationService _authService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationLogService logService,
        INotificationService notificationService,
        INotificationReadService readService,
        IAuthorizationService authService,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationController> logger)
    {
        _logService = logService;
        _notificationService = notificationService;
        _readService = readService;
        _authService = authService;
        _hubContext = hubContext;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst("userId")?.Value ??
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    private string GetCurrentUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value ??
               User.FindFirst("email")?.Value ??
               throw new UnauthorizedAccessException("User email not found in claims");
    }

    private Guid GetCurrentUserGuid()
    {
        var userIdClaim = User.FindFirst("userId")?.Value ??
                         User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
            return userId;

        throw new UnauthorizedAccessException("User ID not found or invalid in claims");
    }

    /// <summary>
    /// Lấy danh sách notification của user hiện tại với read/unread status
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetMyNotifications)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyNotificationsAsync([FromQuery] NotificationRequest request)
    {
        try
        {
            var currentUserEmail = GetCurrentUserEmail();

            // Force filter theo email của user hiện tại để bảo mật
            request.Recipient = currentUserEmail;

            var notifications = await _readService.GetUserNotificationsAsync(
                currentUserEmail,
                request.Page,
                request.Size,
                null); // Lấy tất cả, không filter read/unread

            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user notifications");
            return Problem("Failed to retrieve notifications");
        }
    }

    /// <summary>
    /// Lấy chỉ notification chưa đọc của user hiện tại
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetUnread)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadNotificationsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        try
        {
            var currentUserEmail = GetCurrentUserEmail();

            var notifications = await _readService.GetUserNotificationsAsync(
                currentUserEmail,
                page,
                size,
                false); // Chỉ lấy unread

            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread notifications");
            return Problem("Failed to retrieve unread notifications");
        }
    }

    /// <summary>
    /// Lấy chỉ notification đã đọc của user hiện tại
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetReadNotifications)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(IPaginate<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetReadNotificationsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        try
        {
            var currentUserEmail = GetCurrentUserEmail();

            var notifications = await _readService.GetUserNotificationsAsync(
                currentUserEmail,
                page,
                size,
                true); // Chỉ lấy read

            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get read notifications");
            return Problem("Failed to retrieve read notifications");
        }
    }

    /// <summary>
    /// Lấy notification logs gốc (tương thích với code cũ)
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

    /// <summary>
    /// Đếm số notification chưa đọc
    /// </summary>
    [HttpGet(ApiEndpointConstant.Notification.GetUnreadCount)]
    [CustomAuthorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCountAsync()
    {
        try
        {
            var userEmail = GetCurrentUserEmail();
            var count = await _readService.GetUnreadCountAsync(userEmail);

            return Ok(new
            {
                success = true,
                data = new { unreadCount = count },
                message = "Unread count retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            return Problem("Failed to get unread count");
        }
    }

    /// <summary>
    /// Đánh dấu notification đã đọc và broadcast via SignalR
    /// </summary>
    [HttpPost(ApiEndpointConstant.Notification.MarkAsRead)]
    [CustomAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsReadAsync(Guid id)
    {
        try
        {
            var userEmail = GetCurrentUserEmail();
            var userId = GetCurrentUserGuid();

            await _readService.MarkAsReadAsync(id);

            // 🔄 Real-time update via SignalR
            var unreadCount = await _readService.GetUnreadCountAsync(userEmail);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NotificationRead", new
            {
                NotificationId = id,
                UnreadCount = unreadCount,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("User {UserId} marked notification {NotificationId} as read", userId, id);

            return Ok(new
            {
                success = true,
                data = new { notificationId = id, unreadCount },
                message = "Notification marked as read"
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read", id);
            return Problem("Failed to mark notification as read");
        }
    }

    /// <summary>
    /// Đánh dấu tất cả notification đã đọc và broadcast via SignalR
    /// </summary>
    [HttpPost(ApiEndpointConstant.Notification.MarkAllAsRead)]
    [CustomAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsReadAsync()
    {
        try
        {
            var userEmail = GetCurrentUserEmail();
            var userId = GetCurrentUserGuid();

            await _readService.MarkAllAsReadAsync(userEmail);

            // 🔄 Real-time update via SignalR
            await _hubContext.Clients.User(userId.ToString()).SendAsync("AllNotificationsRead", new
            {
                UnreadCount = 0,
                Timestamp = DateTime.UtcNow,
                Message = "All notifications marked as read"
            });

            _logger.LogInformation("User {UserId} marked all notifications as read", userId);

            return Ok(new
            {
                success = true,
                data = new { unreadCount = 0 },
                message = "All notifications marked as read"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return Problem("Failed to mark all notifications as read");
        }
    }

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
            // Admin có thể xem tất cả mà không filter theo email
            var logs = await _logService.GetNotificationLogsAsync(request);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system notification logs");
            return Problem("Failed to retrieve system notification logs");
        }
    }

    /// <summary>
    /// Test real-time notification connection (for debugging)
    /// </summary>
    [HttpPost(ApiEndpointConstant.Notification.TestConnection)]
    [CustomAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TestConnectionAsync()
    {
        try
        {
            var userId = GetCurrentUserGuid();
            var userEmail = GetCurrentUserEmail();

            // 🔄 Send ping response via SignalR
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ConnectionTest", new
            {
                UserId = userId,
                Email = userEmail,
                Timestamp = DateTime.UtcNow,
                Status = "Connected",
                Message = "SignalR connection test successful"
            });

            return Ok(new
            {
                success = true,
                data = new { userId, userEmail, status = "Connected" },
                message = "Connection test successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection test");
            return Problem("Connection test failed");
        }
    }

    /// <summary>
    /// [ADMIN ONLY] Gửi test notification (for testing SignalR)
    /// </summary>
    [HttpPost(ApiEndpointConstant.Notification.SendTestNotification)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendTestNotificationAsync([FromBody] TestNotificationRequest request)
    {
        try
        {
            var adminUserId = GetCurrentUserGuid();

            // Gửi đến target user hoặc chính admin
            var targetUserId = request.TargetUserId ?? adminUserId;

            await _hubContext.Clients.User(targetUserId.ToString()).SendAsync("ReceiveNotification", new
            {
                Type = "Test",
                Subject = request.Subject ?? "Test Notification",
                Message = request.Message ?? "This is a test notification from admin",
                Timestamp = DateTime.UtcNow,
                DocumentId = Guid.NewGuid(),
                IsTest = true,
                SentBy = adminUserId
            });

            _logger.LogInformation("Test notification sent by admin {AdminId} to user {TargetId}",
                adminUserId, targetUserId);

            return Ok(new
            {
                success = true,
                message = "Test notification sent successfully",
                data = new { targetUserId, sentBy = adminUserId }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test notification");
            return Problem("Failed to send test notification");
        }
    }

    /// <summary>
    /// Gửi general notification tới user cụ thể (integrated với existing service)
    /// </summary>
    [HttpPost(ApiEndpointConstant.Notification.SendGeneral)]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendGeneralNotificationAsync([FromBody] GeneralNotificationRequest request)
    {
        try
        {
            var senderId = GetUserId();

            // Sử dụng existing service
            await _notificationService.SendGeneralNotificationAsync(
                request.TemplateName,
                request.RecipientEmail,
                request.RecipientName);

            _logger.LogInformation("General notification sent by {SenderId} to {RecipientEmail}",
                senderId, request.RecipientEmail);

            return Ok(new
            {
                success = true,
                message = "General notification sent successfully",
                data = new { recipientEmail = request.RecipientEmail, templateName = request.TemplateName }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending general notification");
            return Problem("Failed to send general notification");
        }
    }
}

#region Request Models

public class TestNotificationRequest
{
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public Guid? TargetUserId { get; set; } 
}

public class GeneralNotificationRequest
{
    public required string TemplateName { get; set; }
    public required string RecipientEmail { get; set; }
    public required string RecipientName { get; set; }
}

    #endregion

