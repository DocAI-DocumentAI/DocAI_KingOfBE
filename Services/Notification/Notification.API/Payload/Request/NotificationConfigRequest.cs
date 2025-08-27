using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Notification.Domain.Enums;
using Quartz;

namespace Notification.API.Payload.Request
{
    public class NotificationConfigRequest : IValidatableObject
    {
        [Range(1, 30, ErrorMessage = "Warning threshold must be between 1-30 days")]
        public int WarningThresholdDays { get; set; } = 7;

        [Range(7, 365, ErrorMessage = "Log retention must be between 7-365 days")]
        public int LogRetentionDays { get; set; } = 90;

        public bool QuartzEnabled { get; set; } = true;

        [Required]
        [StringLength(50)]
        public string ExpiredNotificationCron { get; set; } = "0 0 8 * * ?"; // Fixed

        [Required]
        [StringLength(50)]
        public string NearExpiredNotificationCron { get; set; } = "0 0 9 * * ?"; // Fixed

        public bool EnableExpiredNotifications { get; set; } = true;
        public bool EnableNearExpiredNotifications { get; set; } = true;

        // Custom validation method
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Use Quartz's built-in validation
            if (!CronExpression.IsValidExpression(ExpiredNotificationCron))
            {
                results.Add(new ValidationResult(
                    "Invalid expired notification cron expression",
                    new[] { nameof(ExpiredNotificationCron) }));
            }

            if (!CronExpression.IsValidExpression(NearExpiredNotificationCron))
            {
                results.Add(new ValidationResult(
                    "Invalid near-expired notification cron expression",
                    new[] { nameof(NearExpiredNotificationCron) }));
            }

            return results;
        }

    }
}