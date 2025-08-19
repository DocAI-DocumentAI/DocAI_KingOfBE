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
        public const string GoogleLogin = "google/login";
        public const string GoogleCallback = "google/callback";
        public const string GoogleAuthUrl = "google/auth-url";
        public const string RevokeGoogleToken = "google/revoke";
        public const string GoogleRefreshToken = "google/refresh-token";
        public const string CreateUser = "create-user";
        public const string RefreshToken = "refresh-token";
        public const string Logout = "logout";
        public const string ChangePassword = "change-password";
        public const string SendOtp = "send-otp";
        public const string ChangeRole = "change-role";
        public const string ChangeDepartment = "change-department";
        public const string GetUsersByDepartmentAndRole = "get-users-by-department-role";
        public const string Users = "users";
        public const string UpdateProfile = "update-profile";
        public const string AdminUpdateUser = "admin/users";
        public const string UpdateSettings = "settings";
        public const string GetUserById = "user/{userId}";
        public const string GetDepartmentUsers = "department-users";
        public const string ResetPassword = "reset-password";
        public const string ValidateOtp = "check-Otp";
        public const string ForgotPassword = "forgot-password";
    }

    public static class UserPermission
    {
        public const string UserPermissions = "user-permissions";
        public const string AddPermissionToUser = "add-permission-to-user";
        public const string RemovePermissionFromUser = "remove-permission-from-user";
    }

    public class Role
    {
        public const string Roles = "roles";
        public const string RoleInformation = "role";
        public const string CreateRole = "create/role";
        public const string UpdateRole = "update/role";
        public const string DeleteRole = "delete/role";
        public const string AddPermissionToRole = "add-permission-to-role";
    }

    public class Department
    {
        public const string Departments = "departments";
        public const string DepartmentInformation = "department";
        public const string CreateDepartment = "create/department";
        public const string UpdateDepartment = "update/department";
        public const string DeleteDepartment = "delete/department";
    }

    public class Permission
    {
        public const string Permissions = "permissions";
        public const string PermissionInformation = "permission";
        public const string CreatePermission = "create/permission";
        public const string UpdatePermission = "update/permission";
        public const string DeletePermission = "delete/permission";
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
