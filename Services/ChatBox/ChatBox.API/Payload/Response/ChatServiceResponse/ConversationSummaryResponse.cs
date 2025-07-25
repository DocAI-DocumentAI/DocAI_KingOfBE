namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class ConversationSummaryResponse
    {
        public Guid SessionId { get; set; }
        public string Summary { get; set; }
        public List<string> KeyTopics { get; set; } = new();
        public List<string> ActionItems { get; set; } = new();
        public int MessageCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
