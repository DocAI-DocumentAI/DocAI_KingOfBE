namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class ServiceStatus
    {
        public string Status { get; set; }
        public float ResponseTime { get; set; }
        public DateTime LastChecked { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
