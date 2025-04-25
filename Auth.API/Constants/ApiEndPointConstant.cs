namespace Auth.API.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}
    
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/auth";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;
}