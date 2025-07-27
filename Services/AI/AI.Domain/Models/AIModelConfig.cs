using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class AIModelConfig
    {
        [Key]
        public string ModelId { get; set; }
        public string ApiKey { get; set; }
        public string Endpoint { get; set; } = "https://router.huggingface.co/v1/chat/completions";
        public int MaxTokens { get; set; } = 2048;
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 0.9;
        public string Description { get; set; }
        public DateTime ConfiguredAt { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public bool? LastTestResult { get; set; }
        public string LastTestMessage { get; set; }
        public bool IsActive { get; set; }
    }
}