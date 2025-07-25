using ChatBox.API.Services.Implement;

namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class SystemStatusResponse
    {
        public string OverallStatus { get; set; }
        public Dictionary<string, string> Services { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}
