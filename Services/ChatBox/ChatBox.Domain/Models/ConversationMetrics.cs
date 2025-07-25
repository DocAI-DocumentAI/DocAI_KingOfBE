using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ConversationMetrics
    {
        public int TotalConversations { get; set; }
        public int TotalMessages { get; set; }
        public int TotalTokensUsed { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public double AverageConversationLength { get; set; }
        public double AverageResponseTime { get; set; }
    }
}
