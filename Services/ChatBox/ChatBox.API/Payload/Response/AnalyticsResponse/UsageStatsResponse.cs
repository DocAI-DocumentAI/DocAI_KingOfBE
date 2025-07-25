using ChatBox.API.Services.Implement;

namespace ChatBox.API.Payload.Response.AnalyticsResponse
{
    public class UsageStatsResponse
    {
        public DateRange Period { get; set; }
        public int TotalDays { get; set; }
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public int TotalTokens { get; set; }
        public double AverageMessagesPerDay { get; set; }
        public double AverageTokensPerDay { get; set; }
        public float AverageResponseTime { get; set; }
        public Dictionary<string, DailyUsage> DailyBreakdown { get; set; } = new();
    }
}
