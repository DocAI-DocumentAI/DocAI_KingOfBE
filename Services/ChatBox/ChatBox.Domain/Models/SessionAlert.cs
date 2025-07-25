using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SessionAlert
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string AlertType { get; set; } // security, moderation, usage, performance
        public string Severity { get; set; } // low, medium, high, critical
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsResolved { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public virtual ChatSession Session { get; set; }
    }
}
