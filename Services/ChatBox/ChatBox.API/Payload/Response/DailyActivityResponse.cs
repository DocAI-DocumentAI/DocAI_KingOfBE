namespace ChatBox.API.Payload.Response
{
    public class DailyActivityResponse
    {
        public DateTime Date { get; set; }
        public int MessageCount { get; set; }
        public int SessionCount { get; set; }
        public int UniqueUsers { get; set; }
        public long TokensUsed { get; set; }
    }
}
