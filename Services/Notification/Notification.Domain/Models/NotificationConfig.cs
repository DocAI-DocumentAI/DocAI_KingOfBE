
using System.ComponentModel.DataAnnotations;
using Notification.Domain.Enums;

namespace Notification.Domain.Models
{
    public class NotificationConfig : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ConfigKey { get; set; } = "Default";

        public int WarningThresholdDays { get; set; } = 7;
        public int LogRetentionDays { get; set; } = 90;
        public bool QuartzEnabled { get; set; } = true;

        [Required]
        public string ExpiredNotificationCron { get; set; } = "0 0 8 * * ?";     // 8:00 AM daily
        [Required]
        public string NearExpiredNotificationCron { get; set; } = "0 0 9 * * MON"; // 9:00 AM Monday

        public bool EnableExpiredNotifications { get; set; } = true;
        public bool EnableNearExpiredNotifications { get; set; } = true;

        public NotificationMode NearExpiredMode { get; set; } = NotificationMode.Weekly;
    }
}
