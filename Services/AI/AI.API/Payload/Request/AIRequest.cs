

using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class AIRequest
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public List<Document> Documents { get; set; } = new List<Document>();
        public bool StreamResponse { get; set; } = false;

        // AI Settings - will be configured by admin
        public int MaxTokens { get; set; } = 2048;
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 0.9;
    }
}
