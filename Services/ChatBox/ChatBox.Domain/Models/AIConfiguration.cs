using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class AIConfiguration : BaseEntity
    {
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public int MaxTokens { get; set; } = 4000;
        [StringLength(5000, ErrorMessage = "SystemPrompt không được quá 5000 ký tự")]
        public string? SystemPrompt { get; set; }
        public bool IsActive { get; set; }
        public bool IsFree { get; set; } = false;
        public bool IsDefault { get; set; } = false; 
    }
}
