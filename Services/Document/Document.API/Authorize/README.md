# Document Service Authorization Guide

Hướng dẫn sử dụng hệ thống Authorization trong Document Service.

## 🔐 Tổng quan

Document Service sử dụng 2 cách để kiểm tra quyền:

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
  "permissions": "VIEW_ANY_DOCUMENT,CREATE_DOCUMENT,..."
}
```

---

## 🛡️ CustomAuthorizeAttribute

### Cách sử dụng cơ bản

```csharp
// Chỉ cần authenticated
[CustomAuthorize]
public IActionResult GetMyDocuments() { }

// Chỉ Admin
[CustomAuthorize(Roles = new[] { "Admin" })]
public IActionResult AdminOnly() { }

// Admin hoặc Manager
[CustomAuthorize(Roles = new[] { "Admin", "Manager" })]
public IActionResult AdminOrManager() { }

// Chỉ phòng Company
[CustomAuthorize(Departments = new[] { "Company" })]
public IActionResult CompanyOnly() { }

// Có permission VIEW_ANY_DOCUMENT
[CustomAuthorize(Permissions = new[] { "VIEW_ANY_DOCUMENT" })]
public IActionResult ViewAnyDocument() { }
```

### Logic AND vs OR

```csharp
// OR Logic (mặc định) - chỉ cần thỏa mãn 1 điều kiện
[CustomAuthorize(
    Roles = new[] { "Admin" },
    Departments = new[] { "Company" },
    Permissions = new[] { "VIEW_ANY_DOCUMENT" },
    RequireAll = false // mặc định
)]
public IActionResult AdminOrCompanyOrViewPermission() { }

// AND Logic - phải thỏa mãn TẤT CẢ điều kiện
[CustomAuthorize(
    Roles = new[] { "Manager" },
    Departments = new[] { "Company" },
    Permissions = new[] { "MANAGE_DOCUMENTS" },
    RequireAll = true
)]
public IActionResult ManagerAndCompanyAndManagePermission() { }
```

### Constants có sẵn

```csharp
using Auth.API.Attributes;

[CustomAuthorize(Roles = new[] { Roles.Admin })]
[CustomAuthorize(Departments = new[] { Departments.Company })]
[CustomAuthorize(Permissions = new[] { Permissions.ViewAnyDocument })]
```

---

## 🔧 AuthorizationService

### Dependency Injection

```csharp
public class DocumentController : ControllerBase
{
    private readonly IAuthorizationService _authService;

    public DocumentController(IAuthorizationService authService)
    {
        _authService = authService;
    }
}
```

### Lấy thông tin user hiện tại

```csharp
// Lấy thông tin cơ bản
var userId = _authService.GetCurrentUserId();
var userRole = _authService.GetCurrentUserRole();
var userDept = _authService.GetCurrentUserDepartment();
var permissions = _authService.GetCurrentUserPermissions();

// Kiểm tra quyền
bool isAdmin = _authService.HasRole("Admin");
bool inCompany = _authService.IsInDepartment("Company");
bool canView = _authService.HasPermission("VIEW_ANY_DOCUMENT");
bool canViewOrCreate = _authService.HasAnyPermission("VIEW_ANY_DOCUMENT", "CREATE_DOCUMENT");
```

### Conditional Authorization

```csharp
[HttpGet("{id}")]
[CustomAuthorize] // Chỉ cần authenticated
public async Task<IActionResult> GetDocument(Guid id)
{
    var document = await _documentService.GetByIdAsync(id);

    // Logic phức tạp: Admin xem tất cả, User chỉ xem của department mình
    if (!_authService.HasRole("Admin"))
    {
        var userDept = _authService.GetCurrentUserDepartment();
        if (document.DepartmentName != userDept)
        {
            return Forbid("Không thể xem document của department khác");
        }
    }

    return Ok(document);
}
```

### Dynamic Permission Check

```csharp
[HttpPut("{id}")]
[CustomAuthorize]
public async Task<IActionResult> UpdateDocument(Guid id)
{
    var document = await _documentService.GetByIdAsync(id);
    var currentUserId = _authService.GetCurrentUserId();

    // Owner có thể edit, hoặc Admin, hoặc có permission EDIT_ANY_DOCUMENT
    bool canEdit = document.CreatedBy == currentUserId ||
                   _authService.HasRole("Admin") ||
                   _authService.HasPermission("EDIT_ANY_DOCUMENT");

    if (!canEdit)
    {
        return Forbid("Không có quyền chỉnh sửa document này");
    }

    // Proceed with update...
}
```

---

## 📝 Ví dụ thực tế trong Document Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IAuthorizationService _authService;

    public DocumentController(
        IDocumentService documentService,
        IAuthorizationService authService)
    {
        _documentService = documentService;
        _authService = authService;
    }

    // Tất cả user đều upload được draft
    [HttpPost("upload-draft")]
    [CustomAuthorize]
    public async Task<IActionResult> UploadDraft([FromForm] CreateDraftRequest request)
    {
        var userId = _authService.GetCurrentUserId().ToString();
        var result = await _documentService.CreateDraftAsync(request, userId);
        return Ok(result);
    }

    // Chỉ Admin xem được tất cả documents
    [HttpGet("all")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    public async Task<IActionResult> GetAllDocuments()
    {
        var result = await _documentService.GetAllOfficialDocumentsAsync(1, 10);
        return Ok(result);
    }

    // Admin hoặc có permission VIEW_ANY_DOCUMENT
    [HttpGet("official/{id}")]
    [CustomAuthorize(
        Roles = new[] { Roles.Admin },
        Permissions = new[] { Permissions.ViewAnyDocument }
    )]
    public async Task<IActionResult> GetOfficialDocument(string id)
    {
        var result = await _documentService.GetOfficialDocumentAsync(id);
        return Ok(result);
    }

    // Logic phức tạp: User xem documents của department mình
    [HttpGet("department-documents")]
    [CustomAuthorize]
    public async Task<IActionResult> GetDepartmentDocuments()
    {
        // Admin xem tất cả
        if (_authService.HasRole("Admin"))
        {
            var allDocs = await _documentService.GetAllOfficialDocumentsAsync(1, 100);
            return Ok(allDocs);
        }

        // User khác chỉ xem của department mình
        var userDept = _authService.GetCurrentUserDepartment();
        var deptDocs = await _documentService.GetDocumentsByDepartmentAsync(userDept);
        return Ok(deptDocs);
    }

    // Chỉ owner hoặc Admin mới delete được
    [HttpDelete("{id}")]
    [CustomAuthorize]
    public async Task<IActionResult> DeleteDocument(string id, string versionId)
    {
        var document = await _documentService.GetDraftByIdAsync(versionId, "temp");
        var currentUserId = _authService.GetCurrentUserId().ToString();

        // Check ownership hoặc Admin
        if (document.CreatedBy != currentUserId && !_authService.HasRole("Admin"))
        {
            return Forbid("Chỉ được xóa document của chính mình");
        }

        await _documentService.DeleteDraftAsync(id, versionId, currentUserId);
        return Ok("Document deleted successfully");
    }
}
```

---

## ⚠️ Lưu ý quan trọng

### 1. Khi nào dùng CustomAuthorizeAttribute?

- Kiểm tra quyền đơn giản, cố định
- Áp dụng cho toàn bộ action
- Logic OR/AND đơn giản

### 2. Khi nào dùng AuthorizationService?

- Logic authorization phức tạp, conditional
- Kiểm tra quyền dựa trên data (ownership, department matching)
- Cần thông tin user trong business logic
- Multi-level authorization

### 3. Error Handling

```csharp
// CustomAuthorizeAttribute tự động trả về:
// - 401 Unauthorized: Chưa authenticate
// - 403 Forbidden: Không đủ quyền

// AuthorizationService cần handle manual:
if (!_authService.HasPermission("REQUIRED_PERMISSION"))
{
    return Forbid("Custom error message");
}
```

### 4. Performance

- `CustomAuthorizeAttribute`: Nhanh hơn, check ở filter level
- `AuthorizationService`: Chậm hơn một chút, check trong business logic

### 5. Debugging

```csharp
// Log để debug claims
var allClaims = _authService.GetCurrentUser().Claims
    .Select(c => $"{c.Type}: {c.Value}");
Console.WriteLine($"Claims: {string.Join(", ", allClaims)}");
```

---

## 🔗 Tham khảo

- [Auth Service README](../../Auth/Auth.API/Attributes/README.md)
- [Constants](./Attributes/AuthorizeExtensions.cs)
- [Authorization Service Interface](./Services/Interfaces/IAuthorizationService.cs)
