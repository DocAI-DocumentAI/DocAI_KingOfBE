using Notification.API.Payload.Response;
using Notification.Domain.Enums;

namespace Notification.API.Command
{
    public class ProcessDocumentExpirationCommand
    {
        public DocumentDetailResponseExternal Document { get; set; } = null!;
        public NotificationType NotificationType { get; set; }
    }
}
