namespace Notification.Domain.Enums;

public enum NotificationType
{
    NearingExpiration = 1,
    Expired = 2,
    DocumentUpdate = 3,
    SystemMaintenance = 4,
    SystemEscalation = 5,
    General = 6,
    DocumentSubmitted = 7,
    DocumentApproved = 8,
    DocumentRejected = 9

}
