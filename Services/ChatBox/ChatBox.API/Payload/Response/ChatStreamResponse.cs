using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response
{
    public class ChatStreamResponse
    {
        public string SessionId { get; set; }
        public string MessageChunk { get; set; }
        public string Message { get; set; }
        public MessageRole Role { get; set; } = MessageRole.Assistant;
        public DateTime Timestamp { get; set; }
        public string ModelUsed { get; set; }
        public List<DocumentInfo>? DocumentSources { get; set; }
        public bool HasDocumentContext { get; set; } = false;
        public bool IsComplete { get; set; } = false;
        public int? TotalTokenCount { get; set; }
    }
}
