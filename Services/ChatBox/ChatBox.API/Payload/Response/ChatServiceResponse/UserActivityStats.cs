namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class UserActivityStats
    {
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public int TotalTokensUsed { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public double AverageSessionDuration { get; set; }
        public double AverageMessagesPerSession { get; set; }
    }
}
