using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Models
{
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        [ForeignKey("SessionId")]
        public ChatSession Session { get; set; }
        public string Role { get; set; } // user, assistant
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        [Column(TypeName = "vector(1536)")]
        public float[] Embedding { get; set; }
    }
}
