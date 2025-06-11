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
    
    public class Viewer
    {
      public const string Viewers = ApiEndpoint + "/viewers";
      public const string ViewerInformation = ApiEndpoint + "/viewer";
      public const string UpdateViewer = ApiEndpoint + "/update/viewer";
      public const string ResetPassword = ApiEndpoint + "/reset-password";
    }

    public class Editor
    {
        public const string EditorInformation = ApiEndpoint + "/editor";
        public const string Editors = ApiEndpoint + "/editors";
        public const string UpdateEditor = ApiEndpoint + "/update/editor";
    }
}