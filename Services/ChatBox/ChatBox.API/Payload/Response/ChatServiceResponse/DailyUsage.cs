namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class DailyUsage
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        public int MessageCount { get; set; }
        public int TokensUsed { get; set; }
    }
}
