# ChatBox Service Authorization Guide

Hướng dẫn sử dụng hệ thống Authorization trong ChatBox Service.

## 🔐 Tổng quan

ChatBox Service sử dụng 2 cách để kiểm tra quyền:

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
  "permissions": "CHAT_WITH_AI,MANAGE_CHAT_SESSIONS,..."
}
```

---

## 🛡️ CustomAuthorizeAttribute

### Cách sử dụng cơ bản

```csharp
// Chỉ cần authenticated
[CustomAuthorize]
public IActionResult GetMySessions() { }

// Chỉ Admin
[CustomAuthorize(Roles = new[] { "Admin" })]
public IActionResult AdminOnly() { }

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
public IActionResult AdminOrManager() { }

// Có permission CHAT_WITH_AI
[CustomAuthorize(Permissions = new[] { "CHAT_WITH_AI" })]
public IActionResult ChatWithAI() { }
```

---

## 🔧 AuthorizationService

### Dependency Injection

```csharp
public class ChatController : ControllerBase
{
    private readonly IAuthorizationService _authService;

    public ChatController(IAuthorizationService authService)
    {
        _authService = authService;
    }
}
```

---

## 📝 Ví dụ thực tế trong ChatBox Controllers

### SessionController

```csharp
[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IAuthorizationService _authService;

    // Tất cả user đều tạo session được
    [HttpPost]
    [CustomAuthorize]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var userId = _authService.GetCurrentUserId();
        var result = await _sessionService.CreateSessionAsync(request, userId);
        return Ok(result);
    }

    // User chỉ xem sessions của mình, Admin xem tất cả
    [HttpGet]
    [CustomAuthorize]
    public async Task<IActionResult> GetSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (_authService.HasRole("Admin"))
        {
            // Admin xem tất cả sessions
            var allSessions = await _sessionService.GetAllSessionsAsync(page, pageSize);
            return Ok(allSessions);
        }

        // User chỉ xem sessions của mình
        var userId = _authService.GetCurrentUserId();
        var userSessions = await _sessionService.GetUserSessionsAsync(userId, page, pageSize);
        return Ok(userSessions);
    }

    // Chỉ owner hoặc Admin mới xem được session detail
    [HttpGet("{sessionId}")]
    [CustomAuthorize]
    public async Task<IActionResult> GetSessionDetail(Guid sessionId)
    {
        var session = await _sessionService.GetSessionByIdAsync(sessionId);
        var currentUserId = _authService.GetCurrentUserId();

        // Check ownership hoặc Admin
        if (session.UserId != currentUserId && !_authService.HasRole("Admin"))
        {
            return Forbid("Chỉ được xem session của chính mình");
        }

        return Ok(session);
    }

    // Chỉ owner hoặc Admin mới delete được
    [HttpDelete("{sessionId}")]
    [CustomAuthorize]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var session = await _sessionService.GetSessionByIdAsync(sessionId);
        var currentUserId = _authService.GetCurrentUserId();

        if (session.UserId != currentUserId && !_authService.HasRole("Admin"))
        {
            return Forbid("Chỉ được xóa session của chính mình");
        }

        await _sessionService.DeleteSessionAsync(sessionId);
        return Ok("Session deleted successfully");
    }
}
```

### ChatController

```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IAuthorizationService _authService;

    // Cần permission CHAT_WITH_AI
    [HttpPost("send-message")]
    [CustomAuthorize(Permissions = new[] { "CHAT_WITH_AI" })]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = _authService.GetCurrentUserId();

        // Verify session ownership
        var session = await _sessionService.GetSessionByIdAsync(request.SessionId);
        if (session.UserId != userId && !_authService.HasRole("Admin"))
        {
            return Forbid("Không thể gửi message vào session của người khác");
        }

        var result = await _chatService.SendMessageAsync(request, userId);
        return Ok(result);
    }

    // Admin có thể xem tất cả messages, User chỉ xem của mình
    [HttpGet("session/{sessionId}/messages")]
    [CustomAuthorize]
    public async Task<IActionResult> GetMessages(Guid sessionId)
    {
        var session = await _sessionService.GetSessionByIdAsync(sessionId);
        var currentUserId = _authService.GetCurrentUserId();

        if (session.UserId != currentUserId && !_authService.HasRole("Admin"))
        {
            return Forbid("Không thể xem messages của session người khác");
        }

        var messages = await _chatService.GetSessionMessagesAsync(sessionId);
        return Ok(messages);
    }
}
```

### PreferenceController

```csharp
[ApiController]
[Route("api/[controller]")]
public class PreferenceController : ControllerBase
{
    private readonly IPreferenceService _preferenceService;
    private readonly IAuthorizationService _authService;

    // User chỉ xem preference của mình
    [HttpGet]
    [CustomAuthorize]
    public async Task<IActionResult> GetMyPreferences()
    {
        var userId = _authService.GetCurrentUserId();
        var preferences = await _preferenceService.GetUserPreferencesAsync(userId);
        return Ok(preferences);
    }

    // User chỉ update preference của mình
    [HttpPut]
    [CustomAuthorize]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferenceRequest request)
    {
        var userId = _authService.GetCurrentUserId();
        var result = await _preferenceService.UpdatePreferencesAsync(userId, request);
        return Ok(result);
    }

    // Admin xem preferences của user khác
    [HttpGet("user/{userId}")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> GetUserPreferences(Guid userId)
    {
        var preferences = await _preferenceService.GetUserPreferencesAsync(userId);
        return Ok(preferences);
    }
}
```

### AIConfigurationController

```csharp
[ApiController]
[Route("api/[controller]")]
public class AIConfigurationController : ControllerBase
{
    private readonly IAIConfigurationService _aiConfigService;
    private readonly IAuthorizationService _authService;

    // Chỉ Admin mới được xem/quản lý AI config
    [HttpGet]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> GetConfigurations()
    {
        var configs = await _aiConfigService.GetAllConfigurationsAsync();
        return Ok(configs);
    }

    [HttpPost]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> CreateConfiguration([FromBody] AIConfigurationRequest request)
    {
        var result = await _aiConfigService.CreateConfigurationAsync(request);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [CustomAuthorize(Roles = new[] { "Admin" })]
    public async Task<IActionResult> UpdateConfiguration(Guid id, [FromBody] AIConfigurationRequest request)
    {
        var result = await _aiConfigService.UpdateConfigurationAsync(id, request);
        return Ok(result);
    }

    // User có thể xem config để biết model nào available
    [HttpGet("available")]
    [CustomAuthorize]
    public async Task<IActionResult> GetAvailableConfigurations()
    {
        var configs = await _aiConfigService.GetAvailableConfigurationsAsync();

        // Filter sensitive info cho non-admin
        if (!_authService.HasRole("Admin"))
        {
            var filteredConfigs = configs.Select(c => new
            {
                c.Id,
                c.ModelName,
                c.Provider,
                c.IsActive
                // Không trả về ApiKey, Endpoint...
            });
            return Ok(filteredConfigs);
        }

        return Ok(configs);
    }
}
```

---

## ⚠️ ChatBox Service Specific Notes

### 1. Session Ownership

- User chỉ có thể truy cập sessions của mình
- Admin có thể truy cập tất cả sessions
- Luôn verify ownership trước khi thao tác

### 2. AI Chat Permissions

- Cần permission `CHAT_WITH_AI` để chat
- Có thể giới hạn số lượng messages/day cho từng role
- Admin có thể chat unlimited

### 3. Real-time Features (SignalR)

```csharp
[Authorize] // Sử dụng built-in Authorize cho SignalR Hub
public class ChatHub : Hub
{
    private readonly IAuthorizationService _authService;

    public async Task JoinSession(string sessionId)
    {
        var userId = _authService.GetCurrentUserId();
        var session = await _sessionService.GetSessionByIdAsync(Guid.Parse(sessionId));

        // Verify ownership
        if (session.UserId != userId && !_authService.HasRole("Admin"))
        {
            throw new HubException("Không thể join session của người khác");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }
}
```

### 4. Rate Limiting

```csharp
[HttpPost("send-message")]
[CustomAuthorize(Permissions = new[] { "CHAT_WITH_AI" })]
public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
{
    var userId = _authService.GetCurrentUserId();

    // Rate limiting dựa trên role
    if (!_authService.HasRole("Admin"))
    {
        var todayMessageCount = await _chatService.GetTodayMessageCountAsync(userId);
        var maxMessages = _authService.HasRole("Premium") ? 1000 : 100;

        if (todayMessageCount >= maxMessages)
        {
            return BadRequest($"Đã đạt giới hạn {maxMessages} messages/ngày");
        }
    }

    // Proceed...
}
```

---

## 🔗 Tham khảo

- [Auth Service README](../../Auth/Auth.API/Attributes/README.md)
- [Authorization Service Interface](./Services/Interfaces/IAuthorizationService.cs)
- [SignalR Authorization](https://docs.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz)
