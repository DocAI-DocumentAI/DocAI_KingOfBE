using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SessionSummaryResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public SessionStatus Status { get; set; }
        public int MessageCount { get; set; }
        public DateTime LastActivityAt { get; set; }
        public string LastMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
