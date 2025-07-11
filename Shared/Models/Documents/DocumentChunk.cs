using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.Documents
{
    public class DocumentChunk
    {
        public string Id { get; set; }
        public string DocumentId { get; set; }
        public int ChunkOrder{ get; set; }
        public string Text { get; set; }
        public DocumentFile DocumentFile { get; set; }
    }
}
