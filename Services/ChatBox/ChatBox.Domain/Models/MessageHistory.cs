using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class MessageHistory : BaseEntity
    {

        [Required]
        public string ConversationId { get; set; } 

        [ForeignKey("ConversationId")]
        public virtual Conversation Conversation { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string SenderRole { get; set; } 

        [Required]
        public string Content { get; set; } 

        public int Order { get; set; } 

    }
}
