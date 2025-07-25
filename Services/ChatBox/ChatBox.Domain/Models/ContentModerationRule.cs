using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ContentModerationRule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string RuleType { get; set; }
        public string Pattern { get; set; }
        public string Keywords { get; set; } // JSON array
        public string Description { get; set; }
        public double Severity { get; set; }
        public string Action { get; set; }
        public bool IsActive { get; set; }
        public bool IsCaseSensitive { get; set; }
        public bool IsWholeWordOnly { get; set; }
        public string Configuration { get; set; } // JSON
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
