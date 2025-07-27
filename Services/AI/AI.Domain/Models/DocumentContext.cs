using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class DocumentContext
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string? Summary { get; set; }
        public double RelevanceScore { get; set; }
        public string? DocumentType { get; set; }
        public string? DepartmentId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
