using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Command
{
    public class DeactivateDocumentWarningsCommand
    {
        public string DocumentId { get; set; }
        public string Version { get; set; } = string.Empty;
        public Guid RequestId { get; set; } = Guid.NewGuid();
    }
}
