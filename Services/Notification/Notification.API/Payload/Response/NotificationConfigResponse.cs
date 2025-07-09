using System.Text.Json.Serialization;

namespace Notification.API.Payload.Response
{
    public class NotificationConfigResponse
    {
        public string Id { get; set; } = null!;
        public string ConfigKey { get; set; } = null!;
        public int WarningThresholdDays { get; set; }
        public string ScanCronExpression { get; set; } = null!;
        public bool QuartzEnabled { get; set; }
        public int LogRetentionDays { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
    }
}
