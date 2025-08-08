namespace Notification.API.Services.Interfaces
{
    public interface IRateLimitingService
    {
        Task<bool> CanSendEmailAsync();
        Task RecordEmailSentAsync();
        Task<int> GetRemainingEmailsAsync();
    }
}
