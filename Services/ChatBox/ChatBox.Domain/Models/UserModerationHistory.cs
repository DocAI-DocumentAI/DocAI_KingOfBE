using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class UserModerationHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ViolationType { get; set; }
        public string Content { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
        public double Severity { get; set; }
        public DateTime ViolationDate { get; set; }
        public string ReviewStatus { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }
}
