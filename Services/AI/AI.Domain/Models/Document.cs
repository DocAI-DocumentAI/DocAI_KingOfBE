using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class Document
    {
        public string Id { get; set; } // ID của DocumentFile gốc
        public string Content { get; set; } // Phần Text của DocumentChunk
        public string Title { get; set; } // Tiêu đề của DocumentFile
        public string DocumentName { get; set; } // Tên file của DocumentFile
        public string ChunkId { get; set; } // ID của DocumentChunk gốc
    }
}
