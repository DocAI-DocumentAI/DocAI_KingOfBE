using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SecurityRule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string RuleType { get; set; } // pattern, keyword, ml_model, api_call
        public string Pattern { get; set; }
        public string Description { get; set; }
        public double Severity { get; set; }
        public string Action { get; set; } // block, flag, log, ignore
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }
}
