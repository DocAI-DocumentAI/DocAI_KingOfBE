namespace Auth.API.Constants;

public class MessageConstant
{
    public class User
    {
        public const string RegisterFail = "Đăng ký thất bại";
        public const string UserNameExisted = "Tên đăng nhập đã tồn tại";
        public const string PhoneNumberExisted = "Số điện thoại đã tồn tại";
        public const string LoginFailed = "Đăng nhập thất bại";
        public const string UsernameOrPasswork = "Tài khoản hoặc mật khẩu không chính xác";
        public const string LoginRequestNoNull = "Tài khoản hoặc mật khẩu không để trống";
        public const string UserNotFound = "User không tồn tại";
        public const string EmailExisted = "Email đã tồn tại";
        public const string UserNotHaveRole = "User không có role";
    }

    public class DepartmentRolePermission
    {
        public const string DepartmentRolePermissionNotFound = "DepartmentRolePermission không tồn tại";
    }

    public class UserDepartment
    {
        public const string UserDepartmentNotFound = "UserDepartment không tồn tại";
    }

    public class Permission
    {
        public const string PermissionNotFonnd = "Permission không tồn tại";
        public const string PermissionNotNull = "Permission không được để trống";
        public const string PermissionExist = "Permission đã tồn tại";
        public const string CreateFailed = "Create thất bại";
        public const string UpdateFailed = "Update thất bại";
        public const string DeleteFailed = "Delete thất bại";

    }

    public class Role
    {
        public const string RoleNotNull = "Role không được để trống";
        public const string RoleNotFound = "Role không tồn tại";
        public const string RoleExist = "Role đã tồn tại";
        public const string CreateFailed = "Create thất bại";
        public const string UpdateFailed = "Update thất bại";
        public const string DeleteFailed = "Delete thất bại";
    }

    public class UserRole
    {
        public const string UserRoleNotFound = "UserRole không tồn tại";
    }

    public class RolePermission
    {
        public const string RolePermissionNotFound = "RolePermission không tồn tại";
    }

    public class ActivationCode
    {
        public const string ActivationcodeNotFound = "Activation code không đúng";
        public const string CreateActiveKeyFail = "Create activeKey thất bại";
        public const string ActiveKeyUsed = "ActiveKey đã được sử dụng";
    }

    public class Department
    {
        public const string DepartmentNotFound = "Department không tồn tại";
        public const string DepartmentNotNull = "Department không để trống";
        public const string DepartmentExist = "Department đã tồn tại";
        public const string CreateFailed = "Create thất bại";
        public const string UpdateFailed = "Update thất bại";
        public const string DeleteFailed = "Delete thất bại";
    }

    public class OTP
    {
        public const string EmailRequired = "Email không để trống";
        public const string OtpAlreadySent = "OTP đã được gửi trước đó, vui lòng kiểm tra email.";
        public const string SendOtpFailed = "Gửi OTP thất bại";
        public const string SaveOtpFailed = "Lưu OTP thất bại";
        public const string OtpNotFound = "Mã OTP không tồn tại";
        public const string OtpIncorrect = "Mã OTP không chính xác";
    }
    public class Redis
    {
        public const string RedisServiceNotInitialized = "Redis service chưa được khởi tạo.";
    }

    public class Email
    {
        public const string SendEmailFailed = "Gửi Email thất bại";
    }
}