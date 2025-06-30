# Custom Authorization System

Hệ thống authorization tùy chỉnh cho phép kiểm tra Role, Department và Permission một cách linh hoạt.

## Cách sử dụng CustomAuthorizeAttribute

### 1. Kiểm tra Role

```csharp
// Chỉ Admin
[CustomAuthorize(Roles = new[] { "Admin" })]

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { "Admin", "Manager" })]

// Sử dụng constants
[CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
```

### 2. Kiểm tra Department

```csharp
// Chỉ phòng nhân sự
[CustomAuthorize(Departments = new[] { "Phòng nhân sự" })]

// Phòng nhân sự hoặc Company
[CustomAuthorize(Departments = new[] { Departments.PhongNhanSu, Departments.Company })]
```

### 3. Kiểm tra Permission

```csharp
// Chỉ có permission VIEW_ANY_DOCUMENT
[CustomAuthorize(Permissions = new[] { "VIEW_ANY_DOCUMENT" })]

// Có một trong các permissions
[CustomAuthorize(Permissions = new[] { Permissions.ViewAnyDocument, Permissions.ManageUsers })]
```

### 4. Kết hợp nhiều điều kiện

#### Logic OR (mặc định)
```csharp
// Admin HOẶC có permission VIEW_ANY_DOCUMENT HOẶC thuộc HR
[CustomAuthorize(
    Roles = new[] { Roles.Admin },
    Departments = new[] { Departments.PhongNhanSu },
    Permissions = new[] { Permissions.ViewAnyDocument },
    RequireAll = false // mặc định
)]
```

#### Logic AND
```csharp
// Admin VÀ thuộc Company VÀ có permission MANAGE_USERS
[CustomAuthorize(
    Roles = new[] { Roles.Admin },
    Departments = new[] { Departments.Company },
    Permissions = new[] { Permissions.ManageUsers },
    RequireAll = true
)]
```

## Sử dụng IAuthorizationService trong code

### Inject service
```csharp
private readonly IAuthorizationService _authorizationService;

public MyController(IAuthorizationService authorizationService)
{
    _authorizationService = authorizationService;
}
```

### Kiểm tra quyền trong code
```csharp
// Kiểm tra role
if (_authorizationService.HasRole("Admin"))
{
    // Logic cho Admin
}

// Kiểm tra department
if (_authorizationService.IsInDepartment("Phòng nhân sự"))
{
    // Logic cho HR
}

// Kiểm tra permission
if (_authorizationService.HasPermission("VIEW_ANY_DOCUMENT"))
{
    // Logic cho user có permission
}

// Kiểm tra phức tạp
bool hasAccess = _authorizationService.CheckAuthorization(
    roles: new[] { "Admin", "Manager" },
    departments: new[] { "Company" },
    permissions: new[] { "MANAGE_USERS" },
    requireAll: false // OR logic
);
```

### Lấy thông tin user hiện tại
```csharp
var userId = _authorizationService.GetCurrentUserId();
var role = _authorizationService.GetCurrentUserRole();
var department = _authorizationService.GetCurrentUserDepartment();
var permissions = _authorizationService.GetCurrentUserPermissions();
```

## Constants có sẵn

### Roles
- `Roles.Admin`
- `Roles.Manager`
- `Roles.Editor`
- `Roles.Member`

### Departments
- `Departments.Company`
- `Departments.PhongNhanSu`
- `Departments.DepartmentA`
- `Departments.DepartmentB`

### Permissions
- `Permissions.ViewAnyDocument`
- `Permissions.ViewOwnDepartmentDocument`
- `Permissions.CreateDocument`
- `Permissions.EditDocument`
- `Permissions.DeleteDocument`
- `Permissions.ManageUsers`
- `Permissions.ManageRoles`
- `Permissions.ManageDepartments`

## Ví dụ thực tế

### Controller với nhiều endpoints khác nhau
```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;

    public DocumentController(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    // Chỉ Admin mới xem được tất cả documents
    [HttpGet("all")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public IActionResult GetAllDocuments()
    {
        return Ok("All documents");
    }

    // Admin hoặc có permission VIEW_ANY_DOCUMENT
    [HttpGet("view-any")]
    [CustomAuthorize(
        Roles = new[] { Roles.Admin },
        Permissions = new[] { Permissions.ViewAnyDocument }
    )]
    public IActionResult ViewAnyDocument()
    {
        return Ok("View any document");
    }

    // Kiểm tra quyền trong code
    [HttpGet("my-documents")]
    [CustomAuthorize] // Chỉ cần authenticated
    public IActionResult GetMyDocuments()
    {
        if (_authorizationService.HasPermission(Permissions.ViewAnyDocument))
        {
            // Trả về tất cả documents
            return Ok("All documents");
        }
        
        if (_authorizationService.HasPermission(Permissions.ViewOwnDepartmentDocument))
        {
            // Trả về documents của department
            var department = _authorizationService.GetCurrentUserDepartment();
            return Ok($"Documents of {department}");
        }

        // Trả về documents của user
        var userId = _authorizationService.GetCurrentUserId();
        return Ok($"Documents of user {userId}");
    }
}
```

## Lưu ý

1. **JWT Token phải chứa các claims cần thiết:**
   - `ClaimTypes.Role` cho role
   - `"departmentName"` cho department
   - `"permissions"` cho permissions (phân cách bởi dấu phẩy)

2. **Logic OR vs AND:**
   - `RequireAll = false` (mặc định): Chỉ cần thỏa mãn một điều kiện
   - `RequireAll = true`: Phải thỏa mãn tất cả điều kiện

3. **Performance:** Attribute được kiểm tra trước khi vào method, AuthorizationService được sử dụng trong method.

4. **Error Handling:** 
   - Chưa authenticated: 401 Unauthorized
   - Không đủ quyền: 403 Forbidden
