using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ModerationLog
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string Content { get; set; }
        public string ModerationResult { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
        public string ViolatedRules { get; set; } // JSON
        public double ConfidenceScore { get; set; }
        public bool RequiredHumanReview { get; set; }
        public string ReviewStatus { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewComments { get; set; }
        public DateTime CreatedAt { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
