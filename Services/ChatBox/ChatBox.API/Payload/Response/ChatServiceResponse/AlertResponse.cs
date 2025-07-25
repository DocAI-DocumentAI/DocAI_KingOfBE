namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class AlertResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
