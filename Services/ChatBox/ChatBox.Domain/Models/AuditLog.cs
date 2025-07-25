using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string OldValues { get; set; } // JSON
        public string NewValues { get; set; } // JSON
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime Timestamp { get; set; }
        public string SessionId { get; set; }
        public string Source { get; set; } // web, api, system
        public string Category { get; set; } // user_action, system_event, security_event
        public string Severity { get; set; } // low, medium, high, critical
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsDeleted { get; set; }
        public DateTime? RetentionDate { get; set; }
    }
}
