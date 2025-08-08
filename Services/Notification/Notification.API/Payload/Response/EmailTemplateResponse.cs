using System.Text.Json.Serialization;

namespace Notification.API.Payload.Response
{
    public class EmailTemplateResponse
    {
        public Guid Id { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string? AssociatedEvent { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
    }
}
