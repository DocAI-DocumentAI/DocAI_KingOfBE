using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.Documents
{
    public class DocumentTag
    {
        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string TagId { get; set; }
        public DocumentFile DocumentFile { get; set; }
        public Tag Tag { get; set; }

    }
}
