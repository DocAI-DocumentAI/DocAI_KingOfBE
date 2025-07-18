

using System.ComponentModel.DataAnnotations;
using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class AIRequest
    {
        [Required(ErrorMessage = "Question is required")]
        [StringLength(2000, ErrorMessage = "Question cannot exceed 2000 characters")]
        public string Question { get; set; }

        public string SystemPrompt { get; set; }

        public List<Document> Documents { get; set; } = new();

        public string UserId { get; set; }

        [Range(0, 4096, ErrorMessage = "MaxTokens must be between 0 and 4096")]
        public int MaxTokens { get; set; }

        [Range(0, 2, ErrorMessage = "Temperature must be between 0 and 2")]
        public double Temperature { get; set; }

        [Range(0, 1, ErrorMessage = "TopP must be between 0 and 1")]
        public double TopP { get; set; }

        public Dictionary<string, object> Metadata { get; set; }
    }
}
