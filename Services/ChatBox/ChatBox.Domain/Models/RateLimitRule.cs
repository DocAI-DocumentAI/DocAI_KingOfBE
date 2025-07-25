using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class RateLimitRule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Action { get; set; } // send_message, start_streaming, etc.
        public string UserType { get; set; } // standard, premium, admin, all
        public int MaxRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public string WindowType { get; set; } // sliding, fixed
        public bool IsActive { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }
}
