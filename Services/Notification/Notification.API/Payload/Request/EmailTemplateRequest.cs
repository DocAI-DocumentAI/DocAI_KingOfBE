using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notification.API.Payload.Request
{
    public class EmailTemplateRequest
    {
        [Required(ErrorMessage = "Tên template là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên template không được vượt quá 100 ký tự.")]
        public string TemplateName { get; set; } = null!;

        [Required(ErrorMessage = "Tiêu đề email là bắt buộc.")]
        public string Subject { get; set; } = null!;

        [Required(ErrorMessage = "Nội dung HTML là bắt buộc.")]
        public string BodyHtml { get; set; } = null!;

        public string? AssociatedEvent { get; set; }
    }
}
