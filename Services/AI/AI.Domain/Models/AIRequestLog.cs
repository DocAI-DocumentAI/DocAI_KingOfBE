using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Enums;

namespace AI.Domain.Models
{
    public class AIRequestLog
    {
        public int Id { get; set; }
        public string RequestId { get; set; }
        public string UserId { get; set; }
        public ModelType ModelType { get; set; }
        public string RequestContent { get; set; } 
        public string ResponseContent { get; set; } 
        public RequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
