using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ThreatIntelligence
    {
        public Guid Id { get; set; }
        public string ThreatType { get; set; }
        public string Indicator { get; set; }
        public string Source { get; set; }
        public double Confidence { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsActive { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
