using Document.Domain.Enums;
using Document.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class DocumentVersion : BaseEntity
    {
        public DocumentVersion()
        {
            DocumentTags = new HashSet<DocumentTag>();
        }
        public string VersionName { get; set; }
        public string Title { get; set; }
        public string? Summary { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FileHash { get; set; }
        public int? TotalDownloads { get; set; } = 0;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public string? SignedBy { get; set; }
        public StatusEnum Status { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsPublic { get; set; }
        public string DocumentFileId { get; set; }
        public DocumentFile DocumentFile { get; set; }
        public virtual ICollection<DocumentTag> DocumentTags { get; set; }
    }
}
