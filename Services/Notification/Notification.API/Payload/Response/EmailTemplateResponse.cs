using System.Text.Json.Serialization;

namespace Notification.API.Payload.Response
{
    public class EmailTemplateResponse
    {
        public Guid Id { get; set; }
        public string TemplateName { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public string BodyHtml { get; set; } = null!;
        public string? AssociatedEvent { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
    }
}
