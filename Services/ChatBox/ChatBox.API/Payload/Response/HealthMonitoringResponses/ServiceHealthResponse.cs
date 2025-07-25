namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class ServiceHealthResponse
    {
        public string ServiceName { get; set; }
        public string Status { get; set; } // healthy, degraded, unhealthy
        public DateTime LastChecked { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object> HealthChecks { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public string Version { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
