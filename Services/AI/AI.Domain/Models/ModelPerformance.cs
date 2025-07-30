using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class ModelPerformance
    {
        public double AverageResponseTime { get; set; }
        public int TotalRequests { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastUsed { get; set; }
        public DateTime? LastTested { get; set; }
    }
}
