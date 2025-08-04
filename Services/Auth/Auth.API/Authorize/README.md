# Auth Service Authorization Guide

Hướng dẫn sử dụng hệ thống Authorization trong Auth Service.

## 🔐 Tổng quan

Auth Service là **nguồn gốc** của hệ thống authorization. Service này:

1. **Tạo và validate JWT tokens**
2. **Quản lý Users, Roles, Permissions**
3. **Cung cấp CustomAuthorizeAttribute và AuthorizationService** cho các services khác

---

## 📋 JWT Claims Structure

Auth Service tạo JWT với các claims sau:

```json
{
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "User Name",
  "role": "Admin|Manager|Editor|Member",
  "departmentName": "Company|DepartmentA|...",
  "permissions": "VIEW_ANY_DOCUMENT,CREATE_DOCUMENT,MANAGE_USERS,..."
}
```

---

## 🛡️ CustomAuthorizeAttribute

### Cách sử dụng cơ bản

```csharp
// Chỉ cần authenticated
[CustomAuthorize]
public IActionResult GetProfile() { }

// Chỉ Admin
[CustomAuthorize(Roles = new[] { Roles.Admin })]
public IActionResult AdminOnly() { }

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
public IActionResult AdminOrManager() { }

// Chỉ phòng Company
[CustomAuthorize(Departments = new[] { Departments.Company })]
public IActionResult CompanyOnly() { }

// Có permission MANAGE_USERS
[CustomAuthorize(Permissions = new[] { Permissions.ManageUsers })]
public IActionResult ManageUsers() { }
```

### Logic AND vs OR

```csharp
// OR Logic (mặc định) - chỉ cần thỏa mãn 1 điều kiện
[CustomAuthorize(
    Roles = new[] { Roles.Admin },
    Departments = new[] { Departments.Company },
    Permissions = new[] { Permissions.ViewAnyDocument },
    RequireAll = false // mặc định
)]
public IActionResult AdminOrCompanyOrViewPermission() { }

// AND Logic - phải thỏa mãn TẤT CẢ điều kiện
[CustomAuthorize(
    Roles = new[] { Roles.Manager },
    Departments = new[] { Departments.Company },
    Permissions = new[] { Permissions.ManageUsers },
    RequireAll = true
)]
public IActionResult ManagerAndCompanyAndManagePermission() { }
```

---

## 🔧 AuthorizationService

### Dependency Injection

```csharp
public class AuthController : ControllerBase
{
    private readonly IAuthorizationService _authService;

    public AuthController(IAuthorizationService authService)
    {
        _authService = authService;
    }
}
```

---

## 📝 Ví dụ thực tế trong Auth Controllers

### AuthController

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authService;

    // Public endpoints - không cần authorize
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request);
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        return Ok(result);
    }

    // User xem profile của mình
    [HttpGet("profile")]
    [CustomAuthorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = _authService.GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(userId);
        return Ok(profile);
    }

    // User update profile của mình
    [HttpPut("profile")]
    [CustomAuthorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = _authService.GetCurrentUserId();
        var result = await _userService.UpdateProfileAsync(userId, request);
        return Ok(result);
    }

    // User update settings của mình
    [HttpPut("settings")]
    [CustomAuthorize]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingRequest request)
    {
        var result = await _userService.UpdateUserSettingAsync(request);
        return Ok(result);
    }

    // Admin xem user by ID
    [HttpGet("user/{userId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var response = await _userService.GetUserByIdAsync(userId);
        return Ok(response);
    }

    // Admin hoặc Manager xem danh sách users
    [HttpGet("users")]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (_authService.HasRole(Roles.Admin))
        {
            // Admin xem tất cả users
            var allUsers = await _userService.GetAllUsersAsync(page, pageSize);
            return Ok(allUsers);
        }

        // Manager chỉ xem users trong department của mình
        var managerDept = _authService.GetCurrentUserDepartment();
        var deptUsers = await _userService.GetUsersByDepartmentAsync(managerDept, page, pageSize);
        return Ok(deptUsers);
    }

    // Chỉ Admin mới tạo user
    [HttpPost("users")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _userService.CreateUserAsync(request);
        return Ok(result);
    }

    // Admin update bất kỳ user nào, Manager chỉ update users trong department
    [HttpPut("users/{userId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
    {
        // Admin có thể update bất kỳ user nào
        if (_authService.HasRole(Roles.Admin))
        {
            var result = await _userService.UpdateUserAsync(userId, request);
            return Ok(result);
        }

        // Manager chỉ update users trong department của mình
        if (_authService.HasRole(Roles.Manager))
        {
            var targetUser = await _userService.GetUserByIdAsync(userId);
            var managerDept = _authService.GetCurrentUserDepartment();

            if (targetUser.Department?.Name != managerDept)
            {
                return Forbid("Manager chỉ được update users trong department của mình");
            }

            var result = await _userService.UpdateUserAsync(userId, request);
            return Ok(result);
        }

        return Forbid("Insufficient permissions");
    }

    // Chỉ Admin mới delete user
    [HttpDelete("users/{userId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var currentUserId = _authService.GetCurrentUserId();

        // Không thể delete chính mình
        if (userId == currentUserId)
        {
            return BadRequest("Không thể xóa chính mình");
        }

        await _userService.DeleteUserAsync(userId);
        return Ok("User deleted successfully");
    }
}
```

### RoleController

```csharp
[ApiController]
[Route("api/[controller]")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IAuthorizationService _authService;

    // Tất cả user đều xem được danh sách roles (để hiển thị trong UI)
    [HttpGet]
    [CustomAuthorize]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleService.GetAllRolesAsync();

        // Filter sensitive info cho non-admin
        if (!_authService.HasRole(Roles.Admin))
        {
            var filteredRoles = roles.Select(r => new
            {
                r.Id,
                r.RoleName,
                r.Description
                // Không trả về Permissions
            });
            return Ok(filteredRoles);
        }

        return Ok(roles);
    }

    // Chỉ Admin mới tạo/update/delete roles
    [HttpPost]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var result = await _roleService.CreateRoleAsync(request);
        return Ok(result);
    }

    [HttpPut("{roleId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> UpdateRole(Guid roleId, [FromBody] UpdateRoleRequest request)
    {
        var result = await _roleService.UpdateRoleAsync(roleId, request);
        return Ok(result);
    }

    [HttpDelete("{roleId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> DeleteRole(Guid roleId)
    {
        await _roleService.DeleteRoleAsync(roleId);
        return Ok("Role deleted successfully");
    }
}
```

### DepartmentController

```csharp
[ApiController]
[Route("api/[controller]")]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IAuthorizationService _authService;

    // Tất cả user đều xem được departments
    [HttpGet]
    [CustomAuthorize]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _departmentService.GetAllDepartmentsAsync();
        return Ok(departments);
    }

    // Chỉ Admin mới quản lý departments
    [HttpPost]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request)
    {
        var result = await _departmentService.CreateDepartmentAsync(request);
        return Ok(result);
    }

    [HttpPut("{departmentId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> UpdateDepartment(Guid departmentId, [FromBody] UpdateDepartmentRequest request)
    {
        var result = await _departmentService.UpdateDepartmentAsync(departmentId, request);
        return Ok(result);
    }

    [HttpDelete("{departmentId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> DeleteDepartment(Guid departmentId)
    {
        await _departmentService.DeleteDepartmentAsync(departmentId);
        return Ok("Department deleted successfully");
    }

    // Manager xem users trong department của mình
    [HttpGet("{departmentId}/users")]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    public async Task<IActionResult> GetDepartmentUsers(Guid departmentId)
    {
        if (_authService.HasRole(Roles.Admin))
        {
            // Admin xem users của bất kỳ department nào
            var users = await _departmentService.GetDepartmentUsersAsync(departmentId);
            return Ok(users);
        }

        // Manager chỉ xem users của department mình
        var managerDept = _authService.GetCurrentUserDepartment();
        var department = await _departmentService.GetDepartmentByIdAsync(departmentId);

        if (department.Name != managerDept)
        {
            return Forbid("Manager chỉ được xem users trong department của mình");
        }

        var deptUsers = await _departmentService.GetDepartmentUsersAsync(departmentId);
        return Ok(deptUsers);
    }
}
```

### PermissionController

```csharp
[ApiController]
[Route("api/[controller]")]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly IAuthorizationService _authService;

    // Chỉ Admin mới xem/quản lý permissions
    [HttpGet]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _permissionService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    [HttpPost]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        var result = await _permissionService.CreatePermissionAsync(request);
        return Ok(result);
    }

    // Assign permission to user
    [HttpPost("assign-to-user")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> AssignPermissionToUser([FromBody] AssignPermissionRequest request)
    {
        var result = await _permissionService.AssignPermissionToUserAsync(request.UserId, request.PermissionId);
        return Ok(result);
    }

    // Revoke permission from user
    [HttpDelete("revoke-from-user")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> RevokePermissionFromUser([FromBody] RevokePermissionRequest request)
    {
        await _permissionService.RevokePermissionFromUserAsync(request.UserId, request.PermissionId);
        return Ok("Permission revoked successfully");
    }
}
```

---

## ⚠️ Auth Service Specific Notes

### 1. Self-Management vs Admin Management

```csharp
[HttpPut("users/{userId}")]
[CustomAuthorize]
public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
{
    var currentUserId = _authService.GetCurrentUserId();

    // User có thể update chính mình (limited fields)
    if (userId == currentUserId)
    {
        var result = await _userService.UpdateSelfAsync(userId, request);
        return Ok(result);
    }

    // Admin có thể update bất kỳ user nào (full fields)
    if (_authService.HasRole(Roles.Admin))
    {
        var result = await _userService.UpdateUserAsync(userId, request);
        return Ok(result);
    }

    return Forbid("Chỉ được update thông tin của chính mình");
}
```

### 2. Password Management

```csharp
[HttpPut("change-password")]
[CustomAuthorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
{
    var userId = _authService.GetCurrentUserId();
    var result = await _userService.ChangePasswordAsync(userId, request);
    return Ok(result);
}

[HttpPut("reset-password/{userId}")]
[CustomAuthorize(Roles = new[] { Roles.Admin })]
public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request)
{
    var result = await _userService.ResetPasswordAsync(userId, request);
    return Ok(result);
}
```

### 3. Token Management

```csharp
[HttpPost("refresh-token")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    var result = await _userService.RefreshTokenAsync(request);
    return Ok(result);
}

[HttpPost("revoke-token")]
[CustomAuthorize]
public async Task<IActionResult> RevokeToken()
{
    var userId = _authService.GetCurrentUserId();
    await _userService.RevokeTokenAsync(userId);
    return Ok("Token revoked successfully");
}
```

### 4. Audit Logging

```csharp
private async Task LogUserAction(string action, Guid? targetUserId = null)
{
    var currentUserId = _authService.GetCurrentUserId();
    var userRole = _authService.GetCurrentUserRole();

    _logger.LogInformation(
        "User {UserId} (Role: {Role}) performed action: {Action} on target: {TargetUserId}",
        currentUserId, userRole, action, targetUserId
    );
}
```

---

## 🔗 Tham khảo

- [CustomAuthorizeAttribute Implementation](./Attributes/CustomAuthorizeAttribute.cs)
- [AuthorizationService Implementation](./Services/Implement/AuthorizationService.cs)
- [Constants](./Constants/Roles.cs)
- [JWT Utilities](./Utils/JwtUtil.cs)
