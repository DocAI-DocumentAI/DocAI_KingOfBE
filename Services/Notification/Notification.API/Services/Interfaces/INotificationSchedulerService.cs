namespace Notification.API.Services.Interfaces
{
    public interface INotificationSchedulerService
    {
        Task UpdateDocumentScanJobSchedule(string newCronExpression);

    }
}
