namespace Auth.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() { }

    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/auth";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;

    public class User
    {
        public const string Register = ApiEndpoint + "/register";
        public const string SendOtp = ApiEndpoint + "/otp";
        public const string Login = ApiEndpoint + "/login";
        public const string CreateActiveKey = ApiEndpoint + "/create-active-key";
    }

    public class Role
    {
        public const string Roles = ApiEndpoint + "/roles";
        public const string RoleInformation = ApiEndpoint + "/role";
        public const string CreateRole = ApiEndpoint + "create/role";
        public const string UpdateRole = ApiEndpoint + "update/role";
        public const string DeleteRole = ApiEndpoint + "delete/role";
    }
    
    public class Department
    {
        public const string Departments = ApiEndpoint + "/Departments";
        public const string DepartmentInformation = ApiEndpoint + "/Department";
        public const string CreateDepartment = ApiEndpoint + "create/Department";
        public const string UpdateDepartment = ApiEndpoint + "update/Department";
        public const string DeleteDepartment = ApiEndpoint + "delete/Department";
    }
    
    public class Permission
    {
        public const string Permissions = ApiEndpoint + "/Permissions";
        public const string PermissionInformation = ApiEndpoint + "/Permission";
        public const string CreatePermission = ApiEndpoint + "create/Permission";
        public const string UpdatePermission = ApiEndpoint + "update/Permission";
        public const string DeletePermission = ApiEndpoint + "delete/Permission";
    }
}