using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using Auth.API.Payload; // For DescriptionAttribute

// Định nghĩa các loại Permission cần kiểm tra
// Đảm bảo tên trong enum khớp với Permission.Name trong DB
public enum AppPermission
{
    [Description("VIEW_ANY_DOCUMENT")]
    ViewAnyDocument,
    [Description("CREATE_DOCUMENT")]
    CreateDocument,
    [Description("EDIT_OWN_DOCUMENT")]
    EditOwnDocument,
    [Description("EDIT_DEPARTMENT_DOCUMENT")]
    EditDepartmentDocument,
    [Description("APPROVE_DOCUMENT")]
    ApproveDocument,
    // ... thêm các Permission khác theo Permission.Name từ DB
}

// Helper để lấy Description từ Enum
public static class EnumExtensions
{
    public static string GetDescriptionFromEnum(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute));
        return attribute == null ? enumValue.ToString() : attribute.Description;
    }
}


// Attribute tùy chỉnh của bạn để gắn lên các API Controllers/Actions
public class CustomAuthorizeAttribute : TypeFilterAttribute
{
    public CustomAuthorizeAttribute(AppPermission requiredPermission) : base(typeof(CustomAuthorizeFilter))
    {
        Arguments = new object[] { requiredPermission, null }; // Thêm null cho departmentIdParameterName mặc định
    }

    // Constructor overload để truyền DepartmentId nếu quyền phụ thuộc vào Department cụ thể
    public CustomAuthorizeAttribute(AppPermission requiredPermission, string departmentIdParameterName)
        : base(typeof(CustomAuthorizeFilter))
    {
        Arguments = new object[] { requiredPermission, departmentIdParameterName };
    }
}

// CustomAuthorizeFilter sẽ chứa logic kiểm tra quyền
public class CustomAuthorizeFilter : IAsyncActionFilter
{
    private readonly AppPermission _requiredPermission;
    private readonly string _departmentIdParameterName; // Tên tham số chứa DepartmentId trong route/query/body

    public CustomAuthorizeFilter(AppPermission requiredPermission, string departmentIdParameterName)
    {
        _requiredPermission = requiredPermission;
        _departmentIdParameterName = departmentIdParameterName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // 1. Kiểm tra xem người dùng đã được xác thực chưa
        if (!user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Lấy User ID
        var userIdClaim = user.FindFirst("userId");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var currentUserId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Lấy General Roles (ví dụ: Admin)
        var generalRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        // Kiểm tra nếu là Admin (có quyền cao nhất)
        if (generalRoles.Contains("Admin"))
        {
            await next(); // Cho phép truy cập
            return;
        }

        // Lấy Contextual Permissions từ JWT
        var contextualPermissionsClaim = user.FindFirst("contextualPermissions")?.Value;
        if (string.IsNullOrWhiteSpace(contextualPermissionsClaim))
        {
            context.Result = new ForbidResult(); // Không có quyền cụ thể nào
            return;
        }

        List<ContextualPermissionClaim> contextualPermissions;
        try
        {
            contextualPermissions = JsonConvert.DeserializeObject<List<ContextualPermissionClaim>>(contextualPermissionsClaim);
        }
        catch (JsonException)
        {
            context.Result = new UnauthorizedResult(); // JWT payload không hợp lệ
            return;
        }

        // Lấy tên permission cần kiểm tra
        var requiredPermissionName = _requiredPermission.GetDescriptionFromEnum();

        // 2. Xác định DepartmentId của tài nguyên/yêu cầu (nếu cần)
        Guid? resourceDepartmentId = null;
        Guid? resourceCreatorId = null; // Cần thiết cho EDIT_OWN_DOCUMENT

        // Logic để lấy resourceDepartmentId và resourceCreatorId:
        // Cần lấy từ request hoặc từ dữ liệu của Resource Service (bằng cách inject service)
        if (_departmentIdParameterName != null)
        {
            // Thử lấy từ RouteData (ví dụ: /departments/{deptId}/documents)
            if (context.RouteData.Values.TryGetValue(_departmentIdParameterName, out var deptIdValue) && Guid.TryParse(deptIdValue?.ToString(), out var deptIdFromRoute))
            {
                resourceDepartmentId = deptIdFromRoute;
            }
            // Thử lấy từ Query (ví dụ: /documents?deptId={deptId})
            else if (context.HttpContext.Request.Query.ContainsKey(_departmentIdParameterName) && Guid.TryParse(context.HttpContext.Request.Query[_departmentIdParameterName], out var deptIdFromQuery))
            {
                resourceDepartmentId = deptIdFromQuery;
            }
            // Thử lấy từ Action Arguments (ví dụ: nếu deptId là tham số của action)
            else if (context.ActionArguments.ContainsKey(_departmentIdParameterName))
            {
                if (Guid.TryParse(context.ActionArguments[_departmentIdParameterName]?.ToString(), out var deptIdFromArg))
                {
                    resourceDepartmentId = deptIdFromArg;
                }
            }
            // Nếu là thao tác trên một tài liệu cụ thể, bạn cần inject DocumentService
            // và lấy DepartmentId, CreatedByUserId từ tài liệu đó.
            // Example:
            // if (context.ActionArguments.ContainsKey("documentId")) {
            //    var docId = (Guid)context.ActionArguments["documentId"];
            //    var documentService = context.HttpContext.RequestServices.GetService<IDocumentService>(); // Inject service
            //    var document = await documentService.GetDocumentById(docId);
            //    if (document != null) {
            //        resourceDepartmentId = document.DepartmentId;
            //        resourceCreatorId = document.CreatedByUserId;
            //    }
            // }
        }


        // Logic kiểm tra quyền
        bool hasPermission = false;
        foreach (var cpClaim in contextualPermissions)
        {
            // 3. Kiểm tra khớp Department, Role và Permission
            // Nếu quyền yêu cầu một DepartmentId cụ thể, phải khớp cả DeptId trong claim
            if (resourceDepartmentId.HasValue && cpClaim.DeptId != resourceDepartmentId.Value)
            {
                continue; // Bỏ qua nếu DepartmentId không khớp
            }

            if (cpClaim.PermissionName == requiredPermissionName)
            {
                // Kiểm tra các ràng buộc cụ thể của từng Permission
                if (requiredPermissionName == AppPermission.EditOwnDocument.GetDescriptionFromEnum())
                {
                    // Quyền này yêu cầu user là người tạo tài liệu
                    // Cần có resourceCreatorId và khớp với currentUserId
                    hasPermission = resourceCreatorId.HasValue && resourceCreatorId.Value == currentUserId;
                }
                else if (requiredPermissionName == AppPermission.CreateDocument.GetDescriptionFromEnum())
                {
                    // Quyền tạo tài liệu có thể yêu cầu user thuộc dept đó và có vai trò cụ thể
                    // Điều kiện resourceDepartmentId.HasValue đã được check ở trên.
                    hasPermission = true;
                }
                // Thêm các logic kiểm tra ràng buộc khác cho các quyền khác nếu cần.
                else
                {
                    // Đối với các quyền không có ràng buộc đặc biệt (chỉ cần khớp DeptId và PermissionName)
                    hasPermission = true;
                }

                if (hasPermission) break; // Đã tìm thấy quyền, thoát vòng lặp
            }
        }

        if (hasPermission)
        {
            await next(); // Cho phép truy cập action
        }
        else
        {
            context.Result = new ForbidResult(); // Từ chối truy cập (người dùng không có quyền)
        }
    }
}