namespace Auth.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}
    
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/ai";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;
}