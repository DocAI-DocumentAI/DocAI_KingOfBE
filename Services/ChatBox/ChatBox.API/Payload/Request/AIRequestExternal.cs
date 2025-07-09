using ChatBox.Domain.Models;

namespace ChatBox.API.Payload.Request
{
    public class AIRequestExternal
    {
        public string SystemPrompt { get; set; }
        public string Question { get; set; }
        public List<DocumentExternal> Documents { get; set; } = new List<DocumentExternal>();
        public bool StreamResponse { get; set; } = false;
    }
    public class MessageExternal // Đây là phiên bản của Chat.API gửi cho AI.API
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
