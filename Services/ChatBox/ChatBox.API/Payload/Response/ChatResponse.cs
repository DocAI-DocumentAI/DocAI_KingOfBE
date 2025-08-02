using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response
{
    public class ChatResponse
    {
        public string SessionId { get; set; }
        public string Message { get; set; }
        public MessageRole Role { get; set; }
        public int TokenCount { get; set; }
        public DateTime Timestamp { get; set; }
        public string ModelUsed { get; set; }
    }
}
