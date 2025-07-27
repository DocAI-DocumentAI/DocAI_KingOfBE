using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class ModelCapabilities
    {
        public bool SupportsTextGeneration { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsEmbedding { get; set; }
        public int MaxTokens { get; set; }
        public List<string> SupportedLanguages { get; set; }
        public bool SupportsSystemPrompt { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsDocumentAnalysis { get; set; }
    }
}
