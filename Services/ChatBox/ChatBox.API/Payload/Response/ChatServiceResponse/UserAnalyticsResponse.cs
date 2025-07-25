using ChatBox.API.Payload.Response.AnalyticsResponse;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class UserAnalyticsResponse
    {
        public Guid UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public UserActivityStats ActivityStats { get; set; }
        public List<TopicUsage> TopTopics { get; set; } = new();
        public List<DailyUsage> DailyUsage { get; set; } = new();
        public UserEngagementMetrics Engagement { get; set; }
    }
}
