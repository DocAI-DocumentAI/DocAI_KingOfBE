using System.Text.Json.Serialization;
using Notification.Domain.Enums;

namespace Notification.API.Payload.Response
{
    public class NotificationConfigResponse
    {
        public Guid Id { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public int WarningThresholdDays { get; set; }
        public int LogRetentionDays { get; set; }
        public bool QuartzEnabled { get; set; }

        public string ExpiredNotificationCron { get; set; } = string.Empty;
        public string NearExpiredNotificationCron { get; set; } = string.Empty;
        public bool EnableExpiredNotifications { get; set; }
        public bool EnableNearExpiredNotifications { get; set; }
        public NotificationMode NearExpiredMode { get; set; }

        public DateTime CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }

        public DateTime? NextExpiredNotificationTime { get; set; }
        public DateTime? NextNearExpiredNotificationTime { get; set; }
        public string NearExpiredModeDescription => NearExpiredMode.ToString();
    }
}
