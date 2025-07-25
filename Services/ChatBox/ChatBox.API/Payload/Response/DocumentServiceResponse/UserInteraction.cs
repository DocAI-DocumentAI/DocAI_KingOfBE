namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class UserInteraction
    {
        public Guid UserId { get; set; }
        public string InteractionType { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
