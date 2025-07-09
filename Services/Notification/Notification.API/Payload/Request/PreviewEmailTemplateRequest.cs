using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notification.API.Payload.Request
{
    public class PreviewEmailTemplateRequest
    {
        [Required(ErrorMessage = "Template name is required.")]
        public string TemplateName { get; set; } = null!;
        public Dictionary<string, string>? Data { get; set; }
    }
}
