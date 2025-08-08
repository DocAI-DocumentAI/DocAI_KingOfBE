using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notification.API.Payload.Request
{
    public class EmailTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = null!;

        [Required]
        public string Subject { get; set; } = null!;

        [Required]
        public string BodyHtml { get; set; } = null!;

        public string? AssociatedEvent { get; set; }
    }
}
