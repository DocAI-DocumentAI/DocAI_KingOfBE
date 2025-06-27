using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class Conversation : BaseEntity
    {
        [Required]
        public string UserId { get; set; } // Liên kết với người dùng đã đăng nhập (từ Auth Service)

        public string? Title { get; set; } // Tiêu đề ngắn gọn của cuộc hội thoại (ví dụ: "Chat về Chính sách Nghỉ phép")
                                           // Có thể tự động tạo từ câu hỏi đầu tiên

        public DateTime LastActive { get; set; } = DateTime.UtcNow; // Thời điểm hoạt động gần nhất

        // REVIEW POINT: Quan hệ 1-n với MessageHistory
        public virtual ICollection<MessageHistory> MessageHistories { get; set; } = new List<MessageHistory>();
    }
}
