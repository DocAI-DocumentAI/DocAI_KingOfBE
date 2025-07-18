using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class PromptTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public string Category { get; set; }
        public bool IsActive { get; set; }
        public string Variables { get; set; } 
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
