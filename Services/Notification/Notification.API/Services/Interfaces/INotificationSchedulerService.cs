namespace Notification.API.Services.Interfaces
{
    public interface INotificationSchedulerService
    {
        Task UpdateDocumentStatusUpdateJobSchedule(string cronExpression);
        Task UpdateNearExpiredDocumentJobSchedule(string cronExpression);
        Task PauseAllJobs();
        Task ResumeAllJobs();
        Task<object> GetSchedulerStatusAsync();
        Task TriggerNearExpiredDocumentJobNow();
        Task TriggerDocumentStatusUpdateJobNow();
        Task TriggerCleanupJobNow();

    }
}
