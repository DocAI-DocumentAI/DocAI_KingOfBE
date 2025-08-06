namespace ChatBox.API.Payload.Response
{
    public class SessionImpactInfo
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public int MessageCount { get; set; }
        public DateTime LastActivity { get; set; }
        public string Impact { get; set; }
    }
}
