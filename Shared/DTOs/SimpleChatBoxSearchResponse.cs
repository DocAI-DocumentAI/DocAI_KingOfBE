using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class SimpleChatBoxSearchResponse
    {
        public bool Success { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public List<string>? SourceTitles { get; set; }
    }
}
