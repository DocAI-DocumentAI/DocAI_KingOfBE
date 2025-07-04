using Auth.API.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Examples
{
    /// <summary>
    /// Ví dụ về cách sử dụng CustomAuthorizeAttribute
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthorizationExamplesController : ControllerBase
    {
        private readonly Services.Interface.IAuthorizationService _authorizationService;

        public AuthorizationExamplesController(Services.Interface.IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Chỉ Admin mới được truy cập
        /// </summary>
        [HttpGet("admin-only")]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public IActionResult AdminOnly()
        {
            return Ok("Only Admin can access this endpoint");
        }

        /// <summary>
        /// Admin hoặc Manager mới được truy cập
        /// </summary>
        [HttpGet("admin-or-manager")]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        public IActionResult AdminOrManager()
        {
            return Ok("Admin or Manager can access this endpoint");
        }

        /// <summary>
        /// Chỉ người thuộc phòng nhân sự mới được truy cập
        /// </summary>
        [HttpGet("hr-department-only")]
        [CustomAuthorize(Departments = new[] { Departments.PhongNhanSu })]
        public IActionResult HRDepartmentOnly()
        {
            return Ok("Only HR department members can access this endpoint");
        }

        /// <summary>
        /// Chỉ người có permission VIEW_ANY_DOCUMENT mới được truy cập
        /// </summary>
        [HttpGet("view-any-document")]
        [CustomAuthorize(Permissions = new[] { Permissions.ViewAnyDocument })]
        public IActionResult ViewAnyDocument()
        {
            return Ok("Only users with VIEW_ANY_DOCUMENT permission can access this endpoint");
        }

        /// <summary>
        /// Phải là Admin VÀ thuộc Company department VÀ có permission MANAGE_USERS
        /// </summary>
        [HttpGet("admin-and-company-and-manage-users")]
        [CustomAuthorize(
            Roles = new[] { Roles.Admin },
            Departments = new[] { Departments.Company },
            Permissions = new[] { Permissions.ManageUsers },
            RequireAll = true
        )]
        public IActionResult AdminAndCompanyAndManageUsers()
        {
            return Ok("Admin in Company department with MANAGE_USERS permission can access this endpoint");
        }

        /// <summary>
        /// Admin HOẶC có permission VIEW_ANY_DOCUMENT HOẶC thuộc HR department
        /// </summary>
        [HttpGet("admin-or-view-permission-or-hr")]
        [CustomAuthorize(
            Roles = new[] { Roles.Admin },
            Departments = new[] { Departments.PhongNhanSu },
            Permissions = new[] { Permissions.ViewAnyDocument },
            RequireAll = false
        )]
        public IActionResult AdminOrViewPermissionOrHR()
        {
            return Ok("Admin OR users with VIEW_ANY_DOCUMENT permission OR HR department members can access this endpoint");
        }

        /// <summary>
        /// Sử dụng AuthorizationService trong code để kiểm tra quyền
        /// </summary>
        [HttpGet("check-permissions-in-code")]
        [CustomAuthorize] // Chỉ cần authenticated
        public IActionResult CheckPermissionsInCode()
        {
            var currentUser = _authorizationService.GetCurrentUser();
            var userId = _authorizationService.GetCurrentUserId();
            var role = _authorizationService.GetCurrentUserRole();
            var department = _authorizationService.GetCurrentUserDepartment();
            var permissions = _authorizationService.GetCurrentUserPermissions();

            // Kiểm tra quyền trong code
            if (_authorizationService.HasRole(Roles.Admin))
            {
                return Ok(new
                {
                    message = "You are an Admin",
                    userId,
                    role,
                    department,
                    permissions
                });
            }

            if (_authorizationService.HasPermission(Permissions.ViewAnyDocument))
            {
                return Ok(new
                {
                    message = "You have VIEW_ANY_DOCUMENT permission",
                    userId,
                    role,
                    department,
                    permissions
                });
            }

            if (_authorizationService.IsInDepartment(Departments.PhongNhanSu))
            {
                return Ok(new
                {
                    message = "You are in HR department",
                    userId,
                    role,
                    department,
                    permissions
                });
            }

            return Ok(new
            {
                message = "You are authenticated but don't have special permissions",
                userId,
                role,
                department,
                permissions
            });
        }

        /// <summary>
        /// Kiểm tra authorization phức tạp trong code
        /// </summary>
        [HttpGet("complex-authorization-check")]
        [CustomAuthorize] // Chỉ cần authenticated
        public IActionResult ComplexAuthorizationCheck()
        {
            // Kiểm tra: Admin HOẶC (Manager VÀ có permission MANAGE_USERS)
            bool isAdmin = _authorizationService.HasRole(Roles.Admin);
            bool isManagerWithManageUsers = _authorizationService.HasRole(Roles.Manager) &&
                                          _authorizationService.HasPermission(Permissions.ManageUsers);

            if (isAdmin || isManagerWithManageUsers)
            {
                return Ok("You have access to manage users");
            }

            // Kiểm tra: Thuộc HR department VÀ có ít nhất một trong các permissions
            bool isHRWithPermissions = _authorizationService.IsInDepartment(Departments.PhongNhanSu) &&
                                     _authorizationService.HasAnyPermission(
                                         Permissions.ManageUsers,
                                         Permissions.ManageRoles,
                                         Permissions.ManageDepartments
                                     );

            if (isHRWithPermissions)
            {
                return Ok("You are in HR with management permissions");
            }

            return Forbid("You don't have sufficient permissions");
        }
    }
}
