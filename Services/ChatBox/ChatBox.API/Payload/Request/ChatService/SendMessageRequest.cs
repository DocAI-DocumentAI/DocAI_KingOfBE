using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ChatService
{
    public class SendMessageRequest
    {
        public string Message { get; set; }
        public Guid? SessionId { get; set; }
        public string Context { get; set; }
        public bool IncludeSuggestions { get; set; } = true;
        public string AIModelId { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
    }
}
