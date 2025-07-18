using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class ModelProvider
    {
        public int Id { get; set; }
        public string Name { get; set; } // HuggingFace, OpenAI, etc.
        public string BaseUrl { get; set; }
        public string ApiKeyName { get; set; } // Key name in configuration
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public ICollection<ModelConfiguration> ModelConfigurations { get; set; }
    }
}
