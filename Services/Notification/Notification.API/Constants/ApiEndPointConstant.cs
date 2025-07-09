namespace Notification.Api.Constants;

public class ApiEndPointConstant
{
    static ApiEndPointConstant() {}
    
    public const string RootEndPoint = "/api";
    public const string ApiVersion = "/notify";
    public const string ApiEndpoint = RootEndPoint + ApiVersion;

    public static class Notification
    {
        private const string NotificationBase = RootEndPoint + "/notifications";
        public const string Send = NotificationBase + "/send";
        public const string Logs = NotificationBase + "/logs";
        public const string LogById = NotificationBase + "/logs/{id}";
        public const string DismissLog = NotificationBase + "/logs/{id}/dismiss";
    }

    public static class EmailTemplate
    {
        private const string EmailTemplateBase = RootEndPoint + "/email-templates";
        public const string Create = EmailTemplateBase;
        public const string GetById = EmailTemplateBase + "/{id}";
        public const string GetByName = EmailTemplateBase + "/by-name/{templateName}";
        public const string GetAll = EmailTemplateBase;
        public const string Update = EmailTemplateBase + "/{id}";
        public const string Delete = EmailTemplateBase + "/{id}";
        public const string Preview = EmailTemplateBase + "/preview";
    }

    public static class NotificationConfig
    {
        private const string NotificationConfigBase = RootEndPoint + "/notification-config";
        public const string Get = NotificationConfigBase;
        public const string Update = NotificationConfigBase;
    }
}