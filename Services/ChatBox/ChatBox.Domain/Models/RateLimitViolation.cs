using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class RateLimitViolation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Action { get; set; }
        public string RuleName { get; set; }
        public int RequestCount { get; set; }
        public int MaxAllowed { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public DateTime ViolationTime { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Severity { get; set; } // low, medium, high, critical
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string Resolution { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
