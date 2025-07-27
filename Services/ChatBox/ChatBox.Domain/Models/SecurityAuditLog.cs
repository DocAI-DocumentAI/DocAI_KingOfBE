using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SecurityAuditLog
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public string Source { get; set; }
        public string ThreatLevel { get; set; }
        public bool RequiresInvestigation { get; set; }
        public string InvestigationStatus { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchiveDate { get; set; }
        public string Checksum { get; set; }
    }
}
