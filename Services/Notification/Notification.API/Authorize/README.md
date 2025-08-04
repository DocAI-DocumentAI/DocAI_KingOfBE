# Notification Service Authorization Guide

Hướng dẫn sử dụng hệ thống Authorization trong Notification Service.

## 🔐 Tổng quan

Notification Service sử dụng 2 cách để kiểm tra quyền:

1. **CustomAuthorizeAttribute** - Filter level authorization
2. **AuthorizationService** - Business logic level authorization

Cả 2 đều đọc thông tin từ JWT token được gửi từ Auth Service.

---

## 📋 JWT Claims Structure

```json
{
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "User Name",
  "role": "Admin|Manager|Editor|Member",
  "departmentName": "Company|DepartmentA|...",
  "permissions": "SEND_NOTIFICATIONS,MANAGE_NOTIFICATIONS,..."
}
```

---

## 🛡️ CustomAuthorizeAttribute

### Cách sử dụng cơ bản

```csharp
// Chỉ cần authenticated
[CustomAuthorize]
public IActionResult GetMyNotifications() { }

// Chỉ Admin
[CustomAuthorize(Roles = new[] { "Admin" })]
public IActionResult AdminOnly() { }

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
public IActionResult AdminOrManager() { }

// Có permission SEND_NOTIFICATIONS
[CustomAuthorize(Permissions = new[] { "SEND_NOTIFICATIONS" })]
public IActionResult SendNotification() { }
```

---

## 🔧 AuthorizationService

### Dependency Injection

```csharp
public class NotificationController : ControllerBase
{
    private readonly IAuthorizationService _authService;

    public NotificationController(IAuthorizationService authService)
    {
        _authService = authService;
    }
}
```

---

## 📝 Ví dụ thực tế trong Notification Controllers

### NotificationController

```csharp
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IAuthorizationService _authService;

    // User xem notifications của mình
    [HttpGet("my-notifications")]
    [CustomAuthorize]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = _authService.GetCurrentUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);
        return Ok(notifications);
    }

    // Admin xem tất cả notifications
    [HttpGet("all")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> GetAllNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var notifications = await _notificationService.GetAllNotificationsAsync(page, pageSize);
        return Ok(notifications);
    }

    // Manager xem notifications của department mình
    [HttpGet("department")]
    [CustomAuthorize(Roles = new[] { "Manager", "Admin" })]
    public async Task<IActionResult> GetDepartmentNotifications()
    {
        if (_authService.HasRole("Admin"))
        {
            // Admin xem tất cả
            var allNotifications = await _notificationService.GetAllNotificationsAsync(1, 100);
            return Ok(allNotifications);
        }

        // Manager chỉ xem của department mình
        var userDept = _authService.GetCurrentUserDepartment();
        var deptNotifications = await _notificationService.GetDepartmentNotificationsAsync(userDept);
        return Ok(deptNotifications);
    }

    // Cần permission SEND_NOTIFICATIONS
    [HttpPost("send")]
    [CustomAuthorize(Permissions = new[] { "SEND_NOTIFICATIONS" })]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
    {
        var senderId = _authService.GetCurrentUserId();
        var senderRole = _authService.GetCurrentUserRole();
        var senderDept = _authService.GetCurrentUserDepartment();

        // Validate sending scope based on role
        switch (request.Type.ToLower())
        {
            case "system":
                // Chỉ Admin mới được gửi system notification
                if (!_authService.HasRole("Admin"))
                {
                    return Forbid("Chỉ Admin mới được gửi system notification");
                }
                break;

            case "department":
                // Manager chỉ gửi được cho department của mình
                if (_authService.HasRole("Manager") && !_authService.HasRole("Admin"))
                {
                    if (request.TargetDepartment != senderDept)
                    {
                        return Forbid("Manager chỉ được gửi notification cho department của mình");
                    }
                }
                break;

            case "personal":
                // Tất cả đều gửi được personal notification
                break;

            default:
                return BadRequest("Invalid notification type");
        }

        var result = await _notificationService.SendNotificationAsync(request, senderId);
        return Ok(result);
    }

    // User mark notification của mình là đã đọc
    [HttpPut("{notificationId}/mark-read")]
    [CustomAuthorize]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        var userId = _authService.GetCurrentUserId();

        // Verify notification belongs to user
        var notification = await _notificationService.GetNotificationByIdAsync(notificationId);
        if (notification.UserId != userId && !_authService.HasRole("Admin"))
        {
            return Forbid("Chỉ được mark read notification của chính mình");
        }

        await _notificationService.MarkAsReadAsync(notificationId, userId);
        return Ok("Notification marked as read");
    }

    // Admin hoặc sender có thể delete notification
    [HttpDelete("{notificationId}")]
    [CustomAuthorize]
    public async Task<IActionResult> DeleteNotification(Guid notificationId)
    {
        var currentUserId = _authService.GetCurrentUserId();
        var notification = await _notificationService.GetNotificationByIdAsync(notificationId);

        // Check if user is Admin or sender
        bool canDelete = _authService.HasRole("Admin") ||
                        notification.SenderId == currentUserId;

        if (!canDelete)
        {
            return Forbid("Chỉ Admin hoặc người gửi mới được xóa notification");
        }

        await _notificationService.DeleteNotificationAsync(notificationId);
        return Ok("Notification deleted successfully");
    }
}
```

### NotificationTemplateController

```csharp
[ApiController]
[Route("api/[controller]")]
public class NotificationTemplateController : ControllerBase
{
    private readonly INotificationTemplateService _templateService;
    private readonly IAuthorizationService _authService;

    // Tất cả user đều xem được templates
    [HttpGet]
    [CustomAuthorize]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();

        // Filter templates based on role
        if (!_authService.HasRole("Admin"))
        {
            // Non-admin chỉ xem public templates
            templates = templates.Where(t => t.IsPublic).ToList();
        }

        return Ok(templates);
    }

    // Chỉ Admin hoặc Manager mới tạo template
    [HttpPost]
    [CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        var creatorId = _authService.GetCurrentUserId();
        var creatorDept = _authService.GetCurrentUserDepartment();

        // Manager chỉ tạo template cho department của mình
        if (_authService.HasRole("Manager") && !_authService.HasRole("Admin"))
        {
            request.DepartmentScope = creatorDept;
            request.IsPublic = false; // Manager không thể tạo public template
        }

        var result = await _templateService.CreateTemplateAsync(request, creatorId);
        return Ok(result);
    }

    // Chỉ Admin hoặc creator mới update được
    [HttpPut("{templateId}")]
    [CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
    public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] UpdateTemplateRequest request)
    {
        var template = await _templateService.GetTemplateByIdAsync(templateId);
        var currentUserId = _authService.GetCurrentUserId();

        // Check if user can update this template
        bool canUpdate = _authService.HasRole("Admin") ||
                        template.CreatedBy == currentUserId;

        if (!canUpdate)
        {
            return Forbid("Chỉ Admin hoặc creator mới được update template");
        }

        var result = await _templateService.UpdateTemplateAsync(templateId, request);
        return Ok(result);
    }
}
```

### NotificationSettingsController

```csharp
[ApiController]
[Route("api/[controller]")]
public class NotificationSettingsController : ControllerBase
{
    private readonly INotificationSettingsService _settingsService;
    private readonly IAuthorizationService _authService;

    // User xem/update settings của mình
    [HttpGet("my-settings")]
    [CustomAuthorize]
    public async Task<IActionResult> GetMySettings()
    {
        var userId = _authService.GetCurrentUserId();
        var settings = await _settingsService.GetUserSettingsAsync(userId);
        return Ok(settings);
    }

    [HttpPut("my-settings")]
    [CustomAuthorize]
    public async Task<IActionResult> UpdateMySettings([FromBody] UpdateSettingsRequest request)
    {
        var userId = _authService.GetCurrentUserId();
        var result = await _settingsService.UpdateUserSettingsAsync(userId, request);
        return Ok(result);
    }

    // Admin xem settings của user khác
    [HttpGet("user/{userId}")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> GetUserSettings(Guid userId)
    {
        var settings = await _settingsService.GetUserSettingsAsync(userId);
        return Ok(settings);
    }

    // Admin update settings của user khác
    [HttpPut("user/{userId}")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> UpdateUserSettings(Guid userId, [FromBody] UpdateSettingsRequest request)
    {
        var result = await _settingsService.UpdateUserSettingsAsync(userId, request);
        return Ok(result);
    }

    // Admin xem global settings
    [HttpGet("global")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> GetGlobalSettings()
    {
        var settings = await _settingsService.GetGlobalSettingsAsync();
        return Ok(settings);
    }

    [HttpPut("global")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> UpdateGlobalSettings([FromBody] UpdateGlobalSettingsRequest request)
    {
        var result = await _settingsService.UpdateGlobalSettingsAsync(request);
        return Ok(result);
    }
}
```

---

## ⚠️ Notification Service Specific Notes

### 1. Notification Scope

- **Personal**: Gửi cho user cụ thể
- **Department**: Gửi cho tất cả user trong department
- **System**: Gửi cho tất cả user (chỉ Admin)

### 2. Permission Levels

```csharp
// Kiểm tra scope permission
private bool CanSendToScope(string scope)
{
    return scope.ToLower() switch
    {
        "personal" => true, // Ai cũng gửi được
        "department" => _authService.HasRole("Manager") || _authService.HasRole("Admin"),
        "system" => _authService.HasRole("Admin"),
        _ => false
    };
}
```

### 3. Real-time Notifications (SignalR)

```csharp
[Authorize]
public class NotificationHub : Hub
{
    private readonly IAuthorizationService _authService;

    public async Task JoinUserGroup()
    {
        var userId = _authService.GetCurrentUserId().ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    public async Task JoinDepartmentGroup()
    {
        var userDept = _authService.GetCurrentUserDepartment();
        if (!string.IsNullOrEmpty(userDept))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dept_{userDept}");
        }
    }

    // Chỉ Admin mới join system group
    public async Task JoinSystemGroup()
    {
        if (_authService.HasRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "system");
        }
    }
}
```

### 4. Notification Privacy

```csharp
[HttpGet("my-notifications")]
[CustomAuthorize]
public async Task<IActionResult> GetMyNotifications()
{
    var userId = _authService.GetCurrentUserId();
    var notifications = await _notificationService.GetUserNotificationsAsync(userId);

    // Filter sensitive notifications based on role
    if (!_authService.HasRole("Admin"))
    {
        notifications = notifications.Where(n =>
            n.Type != "ADMIN_ONLY" &&
            n.Type != "SYSTEM_CRITICAL"
        ).ToList();
    }

    return Ok(notifications);
}
```

### 5. Bulk Operations

```csharp
[HttpPost("bulk-send")]
[CustomAuthorize(Roles = new[] { "Admin" })]
public async Task<IActionResult> BulkSendNotifications([FromBody] BulkSendRequest request)
{
    var senderId = _authService.GetCurrentUserId();

    // Validate target users
    foreach (var targetUserId in request.TargetUserIds)
    {
        // Admin có thể gửi cho ai cũng được
        // Manager chỉ gửi cho users trong department
        if (_authService.HasRole("Manager") && !_authService.HasRole("Admin"))
        {
            var targetUser = await _userService.GetByIdAsync(targetUserId);
            var senderDept = _authService.GetCurrentUserDepartment();

            if (targetUser.DepartmentName != senderDept)
            {
                return Forbid($"Không thể gửi notification cho user {targetUserId} - khác department");
            }
        }
    }

    var result = await _notificationService.BulkSendAsync(request, senderId);
    return Ok(result);
}
```

---

## 🔗 Tham khảo

- [Auth Service README](../../Auth/Auth.API/Attributes/README.md)
- [Authorization Service Interface](./Services/Interfaces/IAuthorizationService.cs)
- [SignalR Authorization](https://docs.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz)
