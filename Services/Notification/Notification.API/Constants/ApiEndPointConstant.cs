namespace Notification.Api.Constants;

public static class ApiEndpointConstant
{
    static ApiEndpointConstant() { }
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/notification";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;

    public static class EmailTemplate
    {
        public const string GetAll = "templates";
        public const string GetById = "template/{id:guid}";
        public const string GetByName = "template/by-name/{templateName}";
        public const string Create = "template";
        public const string Update = "template/{id:guid}";
        public const string Delete = "template/{id:guid}";
    }

    public static class Notification
    {
        public const string GetLogs = "logs";
        public const string Dismiss = "dismiss/{logId:guid}";
        public const string DismissByToken = "dismiss-by-token";
    }

    public static class Config
    {
        public const string Get = "config";
        public const string Update = "config";
    }
}