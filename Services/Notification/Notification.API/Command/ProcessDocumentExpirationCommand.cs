using Notification.API.Payload.Response;
using Notification.Domain.Enums;
using Shared.Models;

namespace Notification.API.Command
{
    public class ProcessDocumentExpirationCommand
    {
        public DocumentExpirationDto Document { get; set; } = null!;
        public NotificationType NotificationType { get; set; }
    }
}
