using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class DocumentExternal
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string DocumentName { get; set; }
        public string ChunkId { get; set; }
    }
}
