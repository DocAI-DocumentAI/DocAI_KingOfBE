using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class Document
    {
        // Document info
        public string DocumentId { get; set; }
        public string OriginalVersion { get; set; }
        public string Status { get; set; }
        public int? DepartmentId { get; set; }

        // Document Version info
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string VersionCode { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; } // The actual content for AI processing

        // Document Type info
        public string TypeName { get; set; }
        public string TypeDescription { get; set; }

        // Additional metadata
        public string SignedBy { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public bool IsPublic { get; set; }

        // Search relevance
        public double? RelevanceScore { get; set; }

        // File info (if needed)
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
    }
}
