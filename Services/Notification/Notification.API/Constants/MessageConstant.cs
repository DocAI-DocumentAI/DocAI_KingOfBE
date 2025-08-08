namespace Notification.API.Constants;

public static class MessageConstant
{
    public static class EmailTemplate
    {
        public const string CreateSuccess = "Email template created successfully";
        public const string UpdateSuccess = "Email template updated successfully";
        public const string DeleteSuccess = "Email template deleted successfully";
        public const string NotFound = "Email template not found";
        public const string NameExists = "Template name already exists";
    }

    public static class Notification
    {
        public const string DismissSuccess = "Notification dismissed successfully";
        public const string NotFound = "Notification not found";
        public const string AccessDenied = "Access denied to this notification";
    }

    public static class Config
    {
        public const string UpdateSuccess = "Configuration updated successfully";
        public const string InvalidCron = "Invalid cron expression";
        public const string UpdateFailed = "Failed to update configuration";
    }

    public static class Document
    {
        public const string StatusUpdateSuccess = "Document status updated successfully";
        public const string StatusUpdateFailed = "Failed to update document status";
        public const string WarningsDeactivated = "Document warnings deactivated successfully";
        public const string NotFound = "Document not found";
    }
}