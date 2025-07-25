using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class UserUsageStats
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }

        // Daily Usage
        public int MessagesCount { get; set; }
        public int TokensUsed { get; set; }
        public int SessionsCount { get; set; }
        public TimeSpan ActiveTime { get; set; }

        // Rate Limiting
        public int RequestsPerMinute { get; set; }
        public int RequestsPerHour { get; set; }
        public DateTime LastRequestAt { get; set; }

        // Violations
        public int ModerationViolations { get; set; }
        public int RateLimitViolations { get; set; }
        public List<string> ViolationTypes { get; set; } = new();

        // Performance
        public float AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public int SuccessCount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
