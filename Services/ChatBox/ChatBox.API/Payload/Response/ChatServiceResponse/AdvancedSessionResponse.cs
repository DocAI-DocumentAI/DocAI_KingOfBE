using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class AdvancedSessionResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public SessionStatus Status { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public SessionStatistics Statistics { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
