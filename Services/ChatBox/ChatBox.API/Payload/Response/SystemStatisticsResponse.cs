namespace ChatBox.API.Payload.Response
{
    public class SystemStatisticsResponse
    {
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSessions { get; set; }
        public List<ModelUsageStatistics> ModelUsageStats { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }
}
