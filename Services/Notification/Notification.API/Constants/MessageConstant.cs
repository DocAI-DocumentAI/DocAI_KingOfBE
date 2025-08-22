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
        public const string UpdateFailed = "Failed to update notification configuration";
        public const string GetFailed = "Failed to retrieve notification configuration";
        public const string InvalidCronExpression = "Invalid cron expression provided";
        public const string UpdateSuccess = "Notification configuration updated successfully";
        public const string JobTriggered = "Job triggered successfully";
        public const string JobsPaused = "All notification jobs paused";
        public const string JobsResumed = "All notification jobs resumed";
        public const string JobTriggerFailed = "Failed to trigger job";
        public const string JobControlFailed = "Failed to control jobs";
    }

    public static class Document
    {
        public const string StatusUpdateSuccess = "Document status updated successfully";
        public const string StatusUpdateFailed = "Failed to update document status";
        public const string WarningsDeactivated = "Document warnings deactivated successfully";
        public const string NotFound = "Document not found";
    }
}