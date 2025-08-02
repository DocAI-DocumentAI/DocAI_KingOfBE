using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class UserPreference : BaseEntity
    {
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty; 

        [MaxLength(450)]
        public string? SessionId { get; set; } 

        [MaxLength(100)]
        public string? UserName { get; set; } 

        [MaxLength(1000)]
        public string? ChatbotCharacteristics { get; set; } 

        [MaxLength(2000)]
        public string? AdditionalInfo { get; set; } 

        public bool ApplyToNewChats { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;

    }
}
