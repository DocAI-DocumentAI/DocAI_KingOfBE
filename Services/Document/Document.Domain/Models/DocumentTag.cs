using Document.Domain.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class DocumentTag
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DocumentId { get; set; }
        public string TagId { get; set; }
        public DocumentFile DocumentFile { get; set; }
        public Tag Tag { get; set; }

    }
}
