namespace Auth.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}
    
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/auth";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;
    
    public class User
    {
        public const string Register = ApiEndpoint + "/register";
        public const string SendOtp = ApiEndpoint + "/otp";
        public const string Login = ApiEndpoint + "/login";
    }
    
    public class Member
    {
      public const string Members = ApiEndpoint + "/members";
      public const string MemberInformation = ApiEndpoint + "/member";
      public const string UpdateMember = ApiEndpoint + "/update/member";
      public const string ResetPassword = ApiEndpoint + "/reset-password";
    }

    public class Staff
    {
        public const string StaffInformation = ApiEndpoint + "/staff";
        public const string Staffs = ApiEndpoint + "/staffs";
        public const string UpdateStaff = ApiEndpoint + "/update/staff";
    }
}