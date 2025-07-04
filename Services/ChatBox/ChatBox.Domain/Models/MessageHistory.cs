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
        // Id, CreateAt, UpdateAt được kế thừa từ BaseEntity

        [Required]
        public string ConversationId { get; set; } // Foreign Key tới Conversation

        [ForeignKey("ConversationId")]
        public virtual Conversation Conversation { get; set; } = null!; // Navigation property

        [Required]
        [MaxLength(50)] // Ví dụ: "user", "assistant", "system" - đủ dài cho vai trò
        public string SenderRole { get; set; } // Vai trò của người gửi tin nhắn

        [Required]
        public string Content { get; set; } // Nội dung của tin nhắn

        public int Order { get; set; } // Thứ tự tin nhắn trong cuộc hội thoại để duy trì trật tự

    }
}
