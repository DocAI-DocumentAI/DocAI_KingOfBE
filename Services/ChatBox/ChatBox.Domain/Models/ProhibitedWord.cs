using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ProhibitedWord : BaseEntity
    {
        public string Word { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; }
    }
}
