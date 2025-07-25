using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SecurityIncident
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string IncidentType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public string Status { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string DetectionMethod { get; set; }
        public string Evidence { get; set; }
        public string Response { get; set; }
        public string Resolution { get; set; }
        public string AssignedTo { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
