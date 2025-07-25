using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class SecurityMetrics
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public int TotalAnalyses { get; set; }
        public int ThreatsDetected { get; set; }
        public int HighRiskEvents { get; set; }
        public int BlockedRequests { get; set; }
        public int PIIDetections { get; set; }
        public double AverageRiskScore { get; set; }
        public int FalsePositives { get; set; }
        public int TruePositives { get; set; }
        public Dictionary<string, object> DetailedMetrics { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
