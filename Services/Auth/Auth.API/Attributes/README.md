# Custom Authorization System

Hệ thống authorization tùy chỉnh cho phép kiểm tra Role, Department và Permission một cách linh hoạt.

## Cách sử dụng CustomAuthorizeAttribute

### 1. Kiểm tra Role

```csharp
// Chỉ Admin
[CustomAuthorize(Roles = new[] { "Admin" })]
public IActionResult AdminOnly() { }

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
public IActionResult AdminOrManager() { }

// Sử dụng constants
[CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
public IActionResult UsingConstants() { }
```

### 2. Kiểm tra Department

```csharp
// Chỉ phòng nhân sự
[CustomAuthorize(Departments = new[] { "Phòng nhân sự" })]
public IActionResult HROnly() { }

// Phòng nhân sự hoặc Company
[CustomAuthorize(Departments = new[] { Departments.PhongNhanSu, Departments.Company })]
public IActionResult HROrCompany() { }
```

### 3. Kiểm tra Permission (User-based)

```csharp
// Chỉ có permission VIEW_ANY_DOCUMENT
[CustomAuthorize(Permissions = new[] { "VIEW_ANY_DOCUMENT" })]
public IActionResult ViewAnyDocument() { }

// Có một trong các permissions
[CustomAuthorize(Permissions = new[] { Permissions.ViewAnyDocument, Permissions.ManageUsers })]
public IActionResult ViewOrManage() { }

// Kiểm tra nhiều permissions (OR logic)
[CustomAuthorize(Permissions = new[] {
    Permissions.CreateDocument,
    Permissions.EditDocument
})]
public IActionResult CreateOrEdit() { }
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
public IActionResult AdminOrHROrViewPermission() { }
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
public IActionResult AdminAndCompanyAndManageUsers() { }
```

## Ví dụ thực tế trong Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
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

    // Chỉ user có permission CREATE_DOCUMENT
    [HttpPost]
    [CustomAuthorize(Permissions = new[] { Permissions.CreateDocument })]
    public IActionResult CreateDocument()
    {
        return Ok("Document created");
    }

    // Cần có cả CREATE và EDIT permissions
    [HttpPut("{id}")]
    [CustomAuthorize(
        Permissions = new[] { Permissions.CreateDocument, Permissions.EditDocument },
        RequireAll = true
    )]
    public IActionResult UpdateDocument(Guid id)
    {
        return Ok($"Document {id} updated");
    }

    // Manager của HR hoặc Company
    [HttpDelete("{id}")]
    [CustomAuthorize(
        Roles = new[] { Roles.Manager },
        Departments = new[] { Departments.PhongNhanSu, Departments.Company },
        RequireAll = true
    )]
    public IActionResult DeleteDocument(Guid id)
    {
        return Ok($"Document {id} deleted");
    }

    // Chỉ cần authenticated (không kiểm tra gì thêm)
    [HttpGet("my-documents")]
    [CustomAuthorize]
    public IActionResult GetMyDocuments()
    {
        return Ok("My documents");
    }
}
```

## Ví dụ User Management

```csharp
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    // Chỉ Admin mới được tạo user
    [HttpPost]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public IActionResult CreateUser() { }

    // Admin hoặc Manager mới được xem danh sách user
    [HttpGet]
    [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
    public IActionResult GetUsers() { }

    // Cần có permission MANAGE_USERS
    [HttpPut("{id}")]
    [CustomAuthorize(Permissions = new[] { Permissions.ManageUsers })]
    public IActionResult UpdateUser(Guid id) { }

    // Admin VÀ có permission MANAGE_USERS
    [HttpDelete("{id}")]
    [CustomAuthorize(
        Roles = new[] { Roles.Admin },
        Permissions = new[] { Permissions.ManageUsers },
        RequireAll = true
    )]
    public IActionResult DeleteUser(Guid id) { }
}
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

## Lưu ý

1. **Logic OR vs AND:**

   - `RequireAll = false` (mặc định): Chỉ cần thỏa mãn một điều kiện
   - `RequireAll = true`: Phải thỏa mãn tất cả điều kiện

2. **Error Handling:**

   - Chưa authenticated: 401 Unauthorized
   - Không đủ quyền: 403 Forbidden

3. **Permissions từ UserPermissions:**
   - Permissions được gán trực tiếp cho từng user
   - Được lưu trong JWT token và kiểm tra qua claims

```

```
