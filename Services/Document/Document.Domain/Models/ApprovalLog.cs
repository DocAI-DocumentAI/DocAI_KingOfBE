using Document.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class ApprovalLog : BaseEntity
    {
        public ApprovalAction Action { get; set; }
        public string? Comments { get; set; }
        public string DocumentVersionId { get; set; }
        public DocumentVersion DocumentVersion { get; set; }
    }
}
