using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class ChatBoxDocumentRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;

         public string? UserId { get; set; }
         public string? DepartmentId { get; set; }
         public string? Role { get; set; }

        public int MaxResults { get; set; } = 5;
        public double? MinRelevanceScore { get; set; } = 0.7;
        public bool OnlyPublic { get; set; }
        public bool OnlyOfficial { get; set; }
        public List<string>? Tags { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
    }
}
