namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class SecurityEvent
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> EventData { get; set; } = new();
        public string Status { get; set; } // new, investigating, resolved, dismissed
        public string Resolution { get; set; }
    }
}
