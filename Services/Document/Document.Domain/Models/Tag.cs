using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; }
        public virtual ICollection<DocumentTag> DocumentTags { get; set; } = new HashSet<DocumentTag>();    
    }
}
