using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.Documents
{
    public class DocumentVersion
    {
        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string Version { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DocumentFile DocumentFile { get; set; }
    }
}
