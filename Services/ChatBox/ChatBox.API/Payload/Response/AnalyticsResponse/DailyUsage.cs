namespace ChatBox.API.Payload.Response.AnalyticsResponse
{
    public class DailyUsage
    {
        public int Sessions { get; set; }
        public int Messages { get; set; }
        public int Tokens { get; set; }
        public int Errors { get; set; }
    }
}
