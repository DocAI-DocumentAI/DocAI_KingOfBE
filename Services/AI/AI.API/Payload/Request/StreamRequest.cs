using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class StreamRequest
    {
        public string Query { get; set; }
        public string UserId { get; set; }
        public string? Model { get; set; }
        public int? MaxTokens { get; set; } = 2048;
        public double? Temperature { get; set; } = 0.7;
        public double? TopP { get; set; } = 0.9;
        public string? Context { get; set; }
        public List<string>? ConversationHistory { get; set; }
        public string? StreamId { get; set; }
    }
}
