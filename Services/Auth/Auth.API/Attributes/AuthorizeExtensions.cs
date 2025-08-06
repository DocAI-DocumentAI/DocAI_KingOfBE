namespace Auth.API.Attributes
{
    /// <summary>
    /// Extension methods để tạo CustomAuthorizeAttribute dễ dàng hơn
    /// </summary>
    public static class AuthorizeExtensions
    {
        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra roles
        /// </summary>
        public static CustomAuthorizeAttribute WithRoles(params string[] roles)
        {
            return new CustomAuthorizeAttribute { Roles = roles };
        }

        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra departments
        /// </summary>
        public static CustomAuthorizeAttribute WithDepartments(params string[] departments)
        {
            return new CustomAuthorizeAttribute { Departments = departments };
        }

        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra permissions
        /// </summary>
        public static CustomAuthorizeAttribute WithPermissions(params string[] permissions)
        {
            return new CustomAuthorizeAttribute { Permissions = permissions };
        }

        /// <summary>
        /// Tạo CustomAuthorize với logic AND (phải thỏa mãn tất cả điều kiện)
        /// </summary>
        public static CustomAuthorizeAttribute RequireAll(string[]? roles = null, string[]? departments = null, string[]? permissions = null)
        {
            return new CustomAuthorizeAttribute
            {
                Roles = roles,
                Departments = departments,
                Permissions = permissions,
                RequireAll = true
            };
        }

        /// <summary>
        /// Tạo CustomAuthorize với logic OR (chỉ cần thỏa mãn một điều kiện)
        /// </summary>
        public static CustomAuthorizeAttribute RequireAny(string[]? roles = null, string[]? departments = null, string[]? permissions = null)
        {
            return new CustomAuthorizeAttribute
            {
                Roles = roles,
                Departments = departments,
                Permissions = permissions,
                RequireAll = false
            };
        }
    }

    /// <summary>
    /// Constants cho các roles phổ biến
    /// </summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Editor = "Editor";
        public const string Member = "Member";
    }

    /// <summary>
    /// Constants cho các departments phổ biến
    /// </summary>
    public static class Departments
    {
        public const string Company = "Company";
        public const string DepartmentA = "DepartmentA";
    }

    /// <summary>
    /// Constants cho các permissions phổ biến
    /// </summary>
    public static class Permissions
    {
        public const string ViewAnyDocument = "VIEW_ANY_DOCUMENT";
        public const string ViewOwnDepartmentDocument = "VIEW_OWN_DEPARTMENT_DOCUMENT";
        public const string CreateDocument = "CREATE_DOCUMENT";
        public const string EditDocument = "EDIT_DOCUMENT";
        public const string DeleteDocument = "DELETE_DOCUMENT";
    }
}
