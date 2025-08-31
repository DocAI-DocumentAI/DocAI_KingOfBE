using System.Text.Json.Serialization;
using Notification.Domain.Enums;

namespace Notification.API.Payload.Response
{
    public class NotificationConfigResponse
    {
        public Guid Id { get; set; }
        public string ConfigKey { get; set; }
        public int WarningThresholdDays { get; set; }
        public int LogRetentionDays { get; set; }
        public bool QuartzEnabled { get; set; }

        public string NearExpiredNotificationCron { get; set; }
        public string DocumentStatusUpdateCron { get; set; } 
        public bool EnableExpiredNotifications { get; set; }
        public bool EnableNearExpiredNotifications { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
        public DateTime? NextNearExpiredNotificationTime { get; set; }
        public DateTime? NextDocumentStatusUpdateTime { get; set; }
    }
}
