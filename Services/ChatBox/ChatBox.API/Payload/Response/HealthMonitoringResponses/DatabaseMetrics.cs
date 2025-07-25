namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class DatabaseMetrics
    {
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public string ConnectionState { get; set; }
        public string Error { get; set; }
    }
}
