namespace Auth.API.Constants;

public class MessageConstant
{
    public class User
    {
        public const string RegisterFail = "Đăng ký thất bại";
        public const string UserNameExisted = "Tên đăng nhập đã tồn tại";
        public const string PhoneNumberExisted = "Số điện thoại đã tồn tại";
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