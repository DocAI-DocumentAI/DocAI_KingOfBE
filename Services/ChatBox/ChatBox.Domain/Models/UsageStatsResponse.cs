using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class UsageStatsResponse
    {
        public Guid UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public int TotalTokensUsed { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public double AverageSessionDuration { get; set; }
        public double AverageMessagesPerSession { get; set; }
        public Dictionary<string, int> TopTopics { get; set; } = new();
        public List<DailyUsageStats> DailyStats { get; set; } = new();
    }
}
