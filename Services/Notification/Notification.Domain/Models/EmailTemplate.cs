using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Domain.Models
{
    public class EmailTemplate : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string Subject { get; set; } = null!;

        [Required]
        public string BodyHtml { get; set; } = null!;

        [MaxLength(100)]
        public string? AssociatedEvent { get; set; }
    }
}
