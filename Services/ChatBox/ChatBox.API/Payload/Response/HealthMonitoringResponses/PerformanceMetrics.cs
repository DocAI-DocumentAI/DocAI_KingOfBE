using ChatBox.API.Services.Implement;

namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class PerformanceMetrics
    {
        public double ResponseTime { get; set; }
        public double SystemLoad { get; set; }
        public int ConcurrentUsers { get; set; }
        public int ErrorRate { get; set; }
    }
}
