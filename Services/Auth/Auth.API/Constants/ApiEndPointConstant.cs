namespace Auth.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() { }

    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/auth";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;

    public static class User
    {
        public const string Login = "login";
        public const string Register = "register";
        public const string SendOtp = "send-otp";
        public const string ChangeRole = "change-role";
        public const string ChangeDepartment = "change-department";
    }

    public class Role
    {
        public const string Roles = ApiEndpoint + "/roles";
        public const string RoleInformation = ApiEndpoint + "/role";
        public const string CreateRole = ApiEndpoint + "/create/role";
        public const string UpdateRole = ApiEndpoint + "/update/role";
        public const string DeleteRole = ApiEndpoint + "/delete/role";
        public const string AddPermissionToRole = ApiEndpoint + "/add-permission-to-role";
    }

    public class Department
    {
        public const string Departments = ApiEndpoint + "/departments";
        public const string DepartmentInformation = ApiEndpoint + "/department";
        public const string CreateDepartment = ApiEndpoint + "/create/department";
        public const string UpdateDepartment = ApiEndpoint + "/update/department";
        public const string DeleteDepartment = ApiEndpoint + "/delete/department";
    }

    public class Permission
    {
        public const string Permissions = ApiEndpoint + "/permissions";
        public const string PermissionInformation = ApiEndpoint + "/permission";
        public const string CreatePermission = ApiEndpoint + "/create/permission";
        public const string UpdatePermission = ApiEndpoint + "/update/permission";
        public const string DeletePermission = ApiEndpoint + "/delete/permission";
    }

    public class ActiveKey
    {
        public const string ActiveKeys = ApiEndpoint + "/active-keys";
        public const string CreateActiveKey = ApiEndpoint + "/create/active-key";
        public const string GetAllActiveKeys = ApiEndpoint + "/active-keys";
        public const string GetActiveKeyById = ApiEndpoint + "/active-key";
        public const string UpdateActiveKey = ApiEndpoint + "/update/active-key";
        public const string DeleteActiveKey = ApiEndpoint + "/delete/active-key";
    }
}
