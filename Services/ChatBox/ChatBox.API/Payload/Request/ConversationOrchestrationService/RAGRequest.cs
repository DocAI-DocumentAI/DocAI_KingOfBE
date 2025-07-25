using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ConversationOrchestrationService
{
    public class RAGRequest
    {
        [Required]
        public string Query { get; set; }

        [Required]
        public Guid UserId { get; set; }

        public List<string> ConversationHistory { get; set; } = new();
        public Dictionary<string, object> UserPreferences { get; set; } = new();
        public int MaxDocuments { get; set; } = 5;
        public int MaxTokens { get; set; } = 4000;
    }
}
