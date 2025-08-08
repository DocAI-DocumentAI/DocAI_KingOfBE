using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.Infrastructure.Filter;
using Notification.Infrastructure.Paginate;

namespace Notification.API.Services.Interfaces
{
    public interface IEmailTemplateService
    {
        Task<EmailTemplateResponse> CreateEmailTemplateAsync(EmailTemplateRequest request);
        Task<EmailTemplateResponse?> GetEmailTemplateByIdAsync(Guid id);
        Task<EmailTemplateResponse?> GetEmailTemplateByNameAsync(string templateName);
        Task<IPaginate<EmailTemplateResponse>> GetAllEmailTemplatesAsync(int page, int size, string? sortBy, bool isAsc);
        Task<EmailTemplateResponse> UpdateEmailTemplateAsync(Guid id, EmailTemplateRequest request);
        Task<bool> DeleteEmailTemplateAsync(Guid id);
        Task<string> RenderTemplateAsync(string templateName, string userEmail, string userName,
            string documentTitle, string documentVersion, DateTime? effectiveUntil,
            string documentLink, string dismissLink);
    }
}
