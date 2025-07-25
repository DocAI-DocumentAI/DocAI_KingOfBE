using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class EngagementMetrics
    {
        public double AverageRating { get; set; }
        public int FeedbackCount { get; set; }
        public double CompletionRate { get; set; }
        public double ReturnUserRate { get; set; }
        public int FollowUpQuestionRate { get; set; }
    }
}
