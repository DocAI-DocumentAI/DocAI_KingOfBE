using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notification.API.Payload.Request
{
    public class NotificationConfigRequest
    {
        [Range(1, 365, ErrorMessage = "Warning threshold must be between 1 and 365 days.")]
        public int WarningThresholdDays { get; set; }

        [Required(ErrorMessage = "Scan cron expression is required.")]
        public string ScanCronExpression { get; set; } = null!;
        public bool QuartzEnabled { get; set; }

        [Range(30, 1825, ErrorMessage = "Log retention period must be between 30 and 1825 days (5 years).")]
        public int LogRetentionDays { get; set; }
    }
}
