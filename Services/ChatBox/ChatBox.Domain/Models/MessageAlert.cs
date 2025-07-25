using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class MessageAlert
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string AlertType { get; set; } // inappropriate_content, spam, pii_detected, security_risk
        public string Severity { get; set; }
        public string Description { get; set; }
        public bool IsResolved { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();

        public virtual ChatMessage Message { get; set; }
    }
}
