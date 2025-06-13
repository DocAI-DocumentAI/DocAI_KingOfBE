

using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class AIRequest
    {
        public string SystemPrompt { get; set; }
        public string Question { get; set; }
        public List<Document> Documents { get; set; } = new List<Document>(); // List of contextual documents
        public bool StreamResponse { get; set; } = false; // Flag to indicate if streaming response is desired
    }
}
