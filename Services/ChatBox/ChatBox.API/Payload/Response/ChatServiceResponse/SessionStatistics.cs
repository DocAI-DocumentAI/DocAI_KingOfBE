namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SessionStatistics
    {
        public int TotalMessages { get; set; }
        public int TotalTokensUsed { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public double AverageRating { get; set; }
        public List<string> TopTopics { get; set; } = new();
    }
}
