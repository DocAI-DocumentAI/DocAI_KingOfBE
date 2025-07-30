
using System.ComponentModel.DataAnnotations;
using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class AIRequest
    {
        public string Query { get; set; }
        public string UserId { get; set; }
        public string? Model { get; set; }
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public bool Stream { get; set; } = false;
        public string? Context { get; set; }
        public List<string>? ConversationHistory { get; set; }
        public string? Intent { get; set; }
        public string? SessionId { get; set; }
        public string? Source { get; set; }
    }
}
