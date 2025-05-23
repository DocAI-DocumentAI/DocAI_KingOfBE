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
        public string DocumentId { get; set; }
        public string Version { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public DocumentFile DocumentFile { get; set; }
    }
}
