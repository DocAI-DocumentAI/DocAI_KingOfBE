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
        // ✅ Existing endpoints
        public const string GetLogs = "logs";
        public const string Dismiss = "dismiss/{logId:guid}";
        public const string DismissByToken = "dismiss-by-token";
        public const string GetAllSystemLogs = "admin/all-logs";

        public const string GetMyNotifications = "my-notifications";
        public const string GetUnread = "my-notifications/unread";
        public const string GetUnreadCount = "unread-count";
        public const string MarkAsRead = "{id:guid}/mark-read";
        public const string MarkAllAsRead = "mark-all-read";

        public const string TestConnection = "test-connection";
        public const string SendTestNotification = "admin/test-notification";
        public const string SendGeneral = "admin/send-general";

        public const string GetReadNotifications = "my-notifications/read";
        public const string MarkAsUnread = "{id:guid}/mark-unread";

    }

    public static class Config
    {
        public const string Get = "config";
        public const string Update = "config";
    }
}