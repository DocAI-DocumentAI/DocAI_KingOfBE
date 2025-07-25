using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class TopicAnalytics
    {
        public string Topic { get; set; }
        public int MessageCount { get; set; }
        public double Percentage { get; set; }
        public List<string> RelatedKeywords { get; set; } = new();
    }
}
