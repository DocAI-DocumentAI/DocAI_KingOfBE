using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    public class DocumentExpirationDto
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime? EffectiveUntil { get; set; }
        public string Status { get; set; } = string.Empty;
        public string DocumentLink { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }
}
