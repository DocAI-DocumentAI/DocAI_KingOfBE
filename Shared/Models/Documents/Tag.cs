using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models.Documents
{
    public class Tag
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new HashSet<DocumentTag>();    
    }
}
