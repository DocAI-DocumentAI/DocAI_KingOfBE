using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response
{
    public class MessageResponse
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public MessageRole Role { get; set; }
        public int TokenCount { get; set; }
        public DateTime Timestamp { get; set; }
        public string? SessionId { get; set; }
        public string? DocumentSources { get; set; }


    }
}
