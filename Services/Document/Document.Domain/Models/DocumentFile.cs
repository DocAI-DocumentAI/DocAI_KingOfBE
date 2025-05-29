using Document.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Model
{
    public class DocumentFile : BaseEntity
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public string DocumentName { get; set; }

        public string StoragePath { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveUntil { get; set; }

        public string Status { get; set; }
        public virtual ICollection<DocumentVersion> DocumentVersions { get; set; } = new List<DocumentVersion>();
        public virtual ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
        public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new List<DocumentTag>();
    }
}
