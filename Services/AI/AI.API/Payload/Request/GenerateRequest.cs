using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class GenerateRequest
    {
        public string Prompt { get; set; }
        public string UserId { get; set; }
        public string? ModelId { get; set; }
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public List<DocumentContext>? Context { get; set; }
        public List<string>? ConversationHistory { get; set; }
    }
}
