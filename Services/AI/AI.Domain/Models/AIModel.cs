using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{

    public class AIModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsActive { get; set; }

        // Capabilities as direct properties
        public bool SupportsTextGeneration { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsEmbedding { get; set; }
        public int MaxTokens { get; set; }
        public string SupportedLanguages { get; set; } // Comma-separated
        public bool SupportsSystemPrompt { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsDocumentAnalysis { get; set; }

        // Performance as direct properties
        public double AverageResponseTime { get; set; }
        public int TotalRequests { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastUsed { get; set; }
        public DateTime? LastTested { get; set; }

        // Additional properties
        public string? Endpoint { get; set; }
        public string? Description { get; set; }
        public string? ApiVersion { get; set; }
    }
}
