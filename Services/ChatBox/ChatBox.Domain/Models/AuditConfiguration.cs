using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class AuditConfiguration
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; }
        public string ActionType { get; set; }
        public bool IsEnabled { get; set; }
        public int RetentionDays { get; set; }
        public string LogLevel { get; set; } // minimal, standard, detailed, full
        public bool IncludeOldValues { get; set; }
        public bool IncludeNewValues { get; set; }
        public bool RequireApproval { get; set; }
        public List<string> SensitiveFields { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
