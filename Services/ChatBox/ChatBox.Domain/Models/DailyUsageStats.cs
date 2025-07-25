using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class DailyUsageStats
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        public int MessageCount { get; set; }
        public int TokensUsed { get; set; }
        public TimeSpan ActiveTime { get; set; }
    }
}
