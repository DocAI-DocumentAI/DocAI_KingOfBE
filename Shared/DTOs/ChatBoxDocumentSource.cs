using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class ChatBoxDocumentSource
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public double RelevanceScore { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
