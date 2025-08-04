namespace ChatBox.API.Payload.Response
{
    public class UserActivitySummary
    {
        public string UserId { get; set; }
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public long TotalTokensUsed { get; set; }
        public DateTime FirstActivity { get; set; }
        public DateTime LastActivity { get; set; }
        public List<string> ModelsUsed { get; set; } = new();
        public double AverageSessionLength { get; set; }
    }
}
