using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Enums;

namespace AI.Domain.Models
{
    public class ModelConfiguration
    {
        public int Id { get; set; }
        public ModelType ModelType { get; set; }
        public string ModelName { get; set; }
        public string Endpoint { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public bool IsActive { get; set; }
        public int ModelProviderId { get; set; }
        public ModelProvider ModelProvider { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
