using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class ChatBoxDocumentResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string RawContent { get; set; } = string.Empty;
        public string QueryProcessed { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public long ProcessingTimeMs { get; set; }
        public List<ChatBoxDocumentSource> Sources { get; set; } = new();

    }
}
