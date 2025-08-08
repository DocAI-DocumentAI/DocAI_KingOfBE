using System.Text.Json.Serialization;

namespace Notification.API.Payload.Response
{
    public class NotificationConfigResponse
    {
        public Guid Id { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public int WarningThresholdDays { get; set; }
        public string ScanCronExpression { get; set; } = string.Empty;
        public bool QuartzEnabled { get; set; }
        public int LogRetentionDays { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
    }
}
