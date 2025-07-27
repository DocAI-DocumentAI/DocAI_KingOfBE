using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SessionResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public SessionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public int MessageCount { get; set; }
        public string AIModelId { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
