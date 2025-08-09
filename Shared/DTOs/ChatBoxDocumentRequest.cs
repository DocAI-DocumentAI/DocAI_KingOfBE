using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class ChatBoxDocumentRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new List<string>();
        public int MaxResults { get; set; } = 3;
        public double? MinRelevanceScore { get; set; } = 0.28;
        public bool OnlyPublic { get; set; } = false;
        public bool OnlyOfficial { get; set; } = true;
        public List<string>? Tags { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
    }
}
