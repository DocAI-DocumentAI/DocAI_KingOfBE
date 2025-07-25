using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class UserSecurityProfile
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public double RiskScore { get; set; }
        public string RiskLevel { get; set; }
        public List<string> SecurityFlags { get; set; } = new();
        public DateTime LastSecurityCheck { get; set; }
        public int SecurityViolationCount { get; set; }
        public DateTime? LastViolation { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public string BlockReason { get; set; }
        public Dictionary<string, object> SecurityMetrics { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
