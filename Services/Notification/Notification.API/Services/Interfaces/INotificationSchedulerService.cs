namespace Notification.API.Services.Interfaces
{
    public interface INotificationSchedulerService
    {
        Task UpdateDocumentScanJobSchedule(string newCronExpression);
        Task PauseAllJobs();
        Task ResumeAllJobs();
        Task<object> GetSchedulerStatusAsync();
        Task TriggerScanJobNow();
        Task TriggerCleanupJobNow();

    }
}
