namespace ChatBox.API.Attributes
{
    /// <summary>
    /// Extension methods để tạo CustomCusTomAuthorize dễ dàng hơn
    /// </summary>
    public static class AuthorizeExtensions
    {
        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra roles
        /// </summary>
        public static CusTomAuthorize WithRoles(params string[] roles)
        {
            return new CusTomAuthorize { Roles = roles };
        }

        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra departments
        /// </summary>
        public static CusTomAuthorize WithDepartments(params string[] departments)
        {
            return new CusTomAuthorize { Departments = departments };
        }

        /// <summary>
        /// Tạo CustomAuthorize chỉ kiểm tra permissions
        /// </summary>
        public static CusTomAuthorize WithPermissions(params string[] permissions)
        {
            return new CusTomAuthorize { Permissions = permissions };
        }   

        /// <summary>
        /// Tạo CustomAuthorize với logic AND (phải thỏa mãn tất cả điều kiện)
        /// </summary>
        public static CusTomAuthorize RequireAll(string[]? roles = null, string[]? departments = null, string[]? permissions = null)
        {
            return new  CusTomAuthorize
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
        public static CusTomAuthorize RequireAny(string[]? roles = null, string[]? departments = null, string[]? permissions = null)
        {
            return new CusTomAuthorize
            {
                Roles = roles,
                Departments = departments,
                Permissions = permissions,
                RequireAll = false
            };
        }
    }

    /// <summary>
    /// Constants cho các roles phổ biến (Nếu không dùng Shared.Constants)
    /// </summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Editor = "Editor";
        public const string Member = "Member";
    }

    /// <summary>
    /// Constants cho các departments phổ biến (Nếu không dùng Shared.Constants)
    /// </summary>
    public static class Departments
    {
        public const string Company = "Company";
        public const string PhongNhanSu = "Phòng nhân sự";
        public const string DepartmentA = "DepartmentA";
        public const string DepartmentB = "DepartmentB";
    }

    /// <summary>
    /// Constants cho các permissions phổ biến (Nếu không dùng Shared.Constants)
    /// </summary>
    public static class Permissions
    {
        public const string ViewAnyDocument = "VIEW_ANY_DOCUMENT";
        public const string ViewOwnDepartmentDocument = "VIEW_OWN_DEPARTMENT_DOCUMENT";
        public const string CreateDocument = "CREATE_DOCUMENT";
        public const string EditDocument = "EDIT_DOCUMENT";
        public const string DeleteDocument = "DELETE_DOCUMENT";
        public const string ManageUsers = "MANAGE_USERS";
        public const string ManageRoles = "MANAGE_ROLES";
        public const string ManageDepartments = "MANAGE_DEPARTMENTS";
    }
}
