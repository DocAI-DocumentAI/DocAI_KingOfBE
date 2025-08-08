namespace Notification.API.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string recipient, string subject, string body);
        Task<int> GetRemainingEmailsAsync();
    }
}
