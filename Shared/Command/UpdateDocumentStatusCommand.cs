using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class UpdateDocumentStatusCommand
    {
        public Guid DocumentId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}
