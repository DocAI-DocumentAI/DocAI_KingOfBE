using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Enums;

namespace AI.Domain.Models
{
    public class AIRequestLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RequestId { get; set; }

        [MaxLength(50)]
        public string UserId { get; set; }

        [MaxLength(50)]
        public string SourceService { get; set; }

        public ModelType ModelType { get; set; }

        public string? RequestContent { get; set; }

        public string? ResponseContent { get; set; }

        public RequestStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
