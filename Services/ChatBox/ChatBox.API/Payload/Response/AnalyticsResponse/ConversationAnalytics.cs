namespace ChatBox.API.Payload.Response.AnalyticsResponse
{
    public class ConversationAnalytics
    {
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public int TotalTokens { get; set; }
        public TimeSpan TotalChatTime { get; set; }
        public Dictionary<string, int> TopTopics { get; set; } = new();
        public Dictionary<string, int> MessageTypeDistribution { get; set; } = new();
        public Dictionary<string, double> SentimentDistribution { get; set; } = new();
        public double AverageRating { get; set; }
        public Dictionary<string, float> WeeklyActivity { get; set; } = new();
        public Dictionary<string, int> HourlyActivity { get; set; } = new();
        public List<string> MostUsedFeatures { get; set; } = new();
        public List<AnalyticsInsight> Insights { get; set; } = new();
    }
}
