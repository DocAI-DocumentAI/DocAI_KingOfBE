using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class AIConfiguration : BaseEntity
    {
        public string Provider { get; set; } // OpenAI, OpenRouter
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public string Endpoint { get; set; }

        // 3 tham số AI cơ bản
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public double? TopK { get; set; }

        public int MaxTokens { get; set; } = 4000;
        public bool IsActive { get; set; } = true;
        public string SystemPrompt { get; set; } =
        "Bạn là trợ lý AI thông minh chuyên về tìm kiếm tài liệu nội bộ. " +
        "Hãy trả lời bằng tiếng Việt chính xác và hữu ích. " +
        "Khi cần thiết, hãy tìm kiếm tài liệu để đưa ra câu trả lời chính xác nhất.";

        public string CreatedBy { get; set; }
    }
}
