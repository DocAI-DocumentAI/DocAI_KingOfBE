using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ConversationAnalytics
    {
        public Guid UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public ConversationMetrics Metrics { get; set; }
        public List<TopicAnalytics> TopTopics { get; set; } = new();
        public EngagementMetrics Engagement { get; set; }
        public QualityMetrics Quality { get; set; }
    }
}
