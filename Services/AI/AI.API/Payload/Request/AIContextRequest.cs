using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class AIContextRequest
    {
        public string Prompt { get; set; }
        public string UserId { get; set; }
        public List<DocumentContext>? DocumentContext { get; set; }
        public List<string>? ConversationHistory { get; set; }
        public string? Intent { get; set; }
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public bool Stream { get; set; } = false;
        public Dictionary<string, object>? Settings { get; set; }
    }
}
