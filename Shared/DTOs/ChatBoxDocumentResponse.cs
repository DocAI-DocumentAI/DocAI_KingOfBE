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
        public string Answer { get; set; } = string.Empty;
        public List<ChatBoxDocumentSource> Sources { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public long ProcessingTimeMs { get; set; }
        public string QueryProcessed { get; set; } = string.Empty;
    }
}
