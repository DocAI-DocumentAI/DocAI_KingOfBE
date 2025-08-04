using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class SimpleChatBoxSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IncludeSources { get; set; } = false;
        public bool OnlyOfficial { get; set; } = false;
    }
}
