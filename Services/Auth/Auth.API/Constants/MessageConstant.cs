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
    
    public class Role
    {
        public const string RoleNotFound =  "Role không tồn tại";
    }
    
    public class ActivationCode
    {
        public const string ActivationcodeNotFound = "Activation code không đúng";
        public const string CreateActiveKeyFail = "Create activeKey thất bại";
    }
    
    public class Department
    {
        public const string DepartmentNotFound = "Department không tồn tại";
    }
    
    public class Viewer
    {
        public const string ViewerNotFound = "Viewer không tồn tại";
        public const string UpdateFail = "Cập nhật Viewer thất bại";
        public const string ResetPasswordFail = "Reset password Viewer thất bại";
        public const string PasswordOldNotNull = "Password cũ không để trống";
        public const string PasswordNewNotNull = "Password mới không để trống";
        public const string PasswordConfirmNotNull = "Comfirm password để trống";
        public const string PasswordOldWrong = "Password cũ không đúng";
        public const string PasswordConfirmWrong = "Confirm password không đúng";
    }
    
    public class Editor
    {
        public const string EditorNotFound = "Editor không tồn tại";
        public const string UpdateFail = "Cập nhật Editor thất bại";
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