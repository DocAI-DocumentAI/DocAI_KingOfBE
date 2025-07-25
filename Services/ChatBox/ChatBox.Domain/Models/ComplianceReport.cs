using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ComplianceReport
    {
        public Guid Id { get; set; }
        public string ReportType { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string GeneratedBy { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Status { get; set; } // generating, completed, failed
        public string ReportData { get; set; } // JSON
        public string FilePath { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, object> Summary { get; set; } = new();
        public DateTime? ExpiresAt { get; set; }
    }
}
