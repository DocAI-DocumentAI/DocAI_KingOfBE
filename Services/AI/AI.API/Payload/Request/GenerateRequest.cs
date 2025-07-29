using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class GenerateRequest
    {
        public string Query { get; set; }  // Đổi từ Prompt -> Query để match ChatBox
        public string UserId { get; set; }
        public string? Model { get; set; }  // Đổi từ ModelId -> Model
        public int? MaxTokens { get; set; } = 2048;
        public double? Temperature { get; set; } = 0.7;
        public double? TopP { get; set; } = 0.9;
        public string? Context { get; set; }  // Đổi từ List<DocumentContext> -> string
        public List<string>? ConversationHistory { get; set; }
        public Dictionary<string, object>? UserPreferences { get; set; }
        public string? SystemPrompt { get; set; }
    }
}
