
using System.ComponentModel.DataAnnotations;

namespace Notification.Domain.Models
{
    public class NotificationConfig : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ConfigKey { get; set; } = "Default";

        public int WarningThresholdDays { get; set; } = 7;

        [Required]
        public string ScanCronExpression { get; set; } = "0 0 7 * * ?";

        public bool QuartzEnabled { get; set; } = true;

        public int LogRetentionDays { get; set; } = 90;
    }
}
