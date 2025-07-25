namespace ChatBox.API.Payload.Response.HealthMonitoringResponses
{
    public class MemoryMetrics
    {
        public long TotalMemory { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public string Error { get; set; }
    }
}
