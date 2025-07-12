using Document.Domain.Enums;
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
        public string? Description { get; set; }
        public string DepartmentId { get; set; }
        public string OwnerId { get; set; }
        public string? ReplacementId { get; set; }
        public bool IsReplaced { get; set; } = false;
        public ICollection<DocumentVersion> DocumentVersions { get; set; } = new List<DocumentVersion>();
    }
}
