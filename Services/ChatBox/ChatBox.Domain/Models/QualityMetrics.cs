using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class QualityMetrics
    {
        public double ResponseAccuracy { get; set; }
        public double UserSatisfaction { get; set; }
        public int ResolutionRate { get; set; }
        public double AverageHelpfulness { get; set; }
    }
}
