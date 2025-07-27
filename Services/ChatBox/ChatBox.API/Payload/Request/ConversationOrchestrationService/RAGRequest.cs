using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ConversationOrchestrationService
{
    public class RAGRequest
    {
        public string Query { get; set; }
        public Guid UserId { get; set; }
        public List<string> ConversationHistory { get; set; }
        public Dictionary<string, object> UserPreferences { get; set; }
        public string AIModelId { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public int MaxDocuments { get; set; }
        public double MinRelevance { get; set; }
        public string DetectedIntent { get; set; }
    }
}
