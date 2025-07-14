using Document.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class ApprovalClaim : BaseEntity
    {
        public string DocumentVersionId { get; set; }
        public DocumentVersion DocumentVersion { get; set; }
        public DateTime ClaimedAt { get; set; }
        public string ClaimedBy { get; set; }
        public bool IsActive { get; set; }
    }
}
