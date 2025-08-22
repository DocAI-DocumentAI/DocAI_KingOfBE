namespace Notification.API.Services.Interfaces
{
    public interface INotificationSchedulerService
    {
        Task UpdateExpiredDocumentJobSchedule(string cronExpression);
        Task UpdateNearExpiredDocumentJobSchedule(string cronExpression);
        Task PauseAllJobs();
        Task ResumeAllJobs();
        Task<object> GetSchedulerStatusAsync();
        Task TriggerExpiredDocumentJobNow();
        Task TriggerNearExpiredDocumentJobNow();
        Task TriggerCleanupJobNow();

    }
}
