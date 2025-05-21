using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class EmbeddingModel
    {
         [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public int Dimensions { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
