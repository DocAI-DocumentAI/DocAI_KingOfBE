namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class UserEngagementMetrics
    {
        public double AverageRating { get; set; }
        public int FeedbackCount { get; set; }
        public double SessionCompletionRate { get; set; }
        public double ReturnUserRate { get; set; }
    }
}
