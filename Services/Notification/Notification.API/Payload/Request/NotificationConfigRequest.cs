using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Notification.Domain.Enums;
using Quartz;

namespace Notification.API.Payload.Request
{
    public class NotificationConfigRequest : IValidatableObject
    {
        [Range(1, 365)]
        public int WarningThresholdDays { get; set; }

        [Range(1, 3650)]
        public int LogRetentionDays { get; set; }

        public bool QuartzEnabled { get; set; }

        // REMOVED: public string ExpiredNotificationCron { get; set; }

        [Required]
        public string NearExpiredNotificationCron { get; set; }

        [Required]
        public string DocumentStatusUpdateCron { get; set; } // NEW

        public bool EnableExpiredNotifications { get; set; }
        public bool EnableNearExpiredNotifications { get; set; }

        // Custom validation method
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // Use Quartz's built-in validation
            if (!CronExpression.IsValidExpression(DocumentStatusUpdateCron))
            {
                results.Add(new ValidationResult(
                    "Invalid expired notification cron expression",
                    new[] { nameof(DocumentStatusUpdateCron) }));
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