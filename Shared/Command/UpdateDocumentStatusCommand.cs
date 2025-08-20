using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class UpdateDocumentStatusCommand
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public bool UpdateKernelMemory { get; set; } = true;
        public DateTime? VietnamTime { get; set; }
        public string UpdatedBy { get; set; } = "system_notification";
    }
}
