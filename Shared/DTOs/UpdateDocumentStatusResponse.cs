using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class UpdateDocumentStatusResponse
    {
        public bool Success { get; set; }   
        public string? ErrorMessage { get; set; }
        public Guid RequestId { get; set; }

        public string? DocumentId { get; set; }
        public string? Version { get; set; }
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public bool KernelMemoryUpdated { get; set; }
    }
}
