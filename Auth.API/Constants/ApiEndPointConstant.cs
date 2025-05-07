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
      public const string MemberInformation = ApiEndpoint + "/member";
    }
}