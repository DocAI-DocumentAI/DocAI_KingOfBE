using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Notification.Domain.Enums;

namespace Notification.API.Payload.Request
{
    public class NotificationConfigRequest
    {
        [Range(1, 30)]
        public int WarningThresholdDays { get; set; } = 7;

        [Range(7, 365)]
        public int LogRetentionDays { get; set; } = 90;

        public bool QuartzEnabled { get; set; } = true;

        [Required]
        public string ExpiredNotificationCron { get; set; } = "0 0 8 * * ?";

        [Required]
        public string NearExpiredNotificationCron { get; set; } = "0 0 9 * * MON";

        public bool EnableExpiredNotifications { get; set; } = true;
        public bool EnableNearExpiredNotifications { get; set; } = true;

        public NotificationMode NearExpiredMode { get; set; } = NotificationMode.Weekly;
    }
}