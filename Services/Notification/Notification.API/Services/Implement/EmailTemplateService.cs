using AutoMapper;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Models;
using Notification.Infrastructure.Filter;
using Notification.Infrastructure.Paginate;
using Notification.Infrastructure.Repository.Interfaces;



namespace Notification.API.Services.Implement
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<EmailTemplateService> _logger;

        public EmailTemplateService(IUnitOfWork<NotificationDbContext> unitOfWork, IMapper mapper, ILogger<EmailTemplateService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<EmailTemplateResponse> CreateEmailTemplateAsync(EmailTemplateRequest request)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();

            var exists = await repo.AnyAsync(t => t.TemplateName == request.TemplateName);
            if (exists)
                throw new BadHttpRequestException($"Template '{request.TemplateName}' already exists");

            var template = _mapper.Map<EmailTemplate>(request);
            await repo.InsertAsync(template);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created email template: {TemplateName}", template.TemplateName);
            return _mapper.Map<EmailTemplateResponse>(template);
        }

        public async Task<EmailTemplateResponse?> GetEmailTemplateByIdAsync(Guid id)
        {
            var template = await _unitOfWork.GetRepository<EmailTemplate>()
                .SingleOrDefaultAsync(predicate: t => t.Id == id);

            return template != null ? _mapper.Map<EmailTemplateResponse>(template) : null;
        }

        public async Task<EmailTemplateResponse?> GetEmailTemplateByNameAsync(string templateName)
        {
            var template = await _unitOfWork.GetRepository<EmailTemplate>()
                .SingleOrDefaultAsync(predicate: t => t.TemplateName == templateName);

            return template != null ? _mapper.Map<EmailTemplateResponse>(template) : null;
        }

        public async Task<IPaginate<EmailTemplateResponse>> GetAllEmailTemplatesAsync(int page, int size, string? sortBy, bool isAsc)
        {
            var templates = await _unitOfWork.GetRepository<EmailTemplate>().GetPagingListAsync(
                selector: t => _mapper.Map<EmailTemplateResponse>(t),
                filter: null,
                page: page,
                size: size,
                sortBy: sortBy ?? "CreateAt",
                isAsc: isAsc
            );

            return templates;
        }

        public async Task<EmailTemplateResponse> UpdateEmailTemplateAsync(Guid id, EmailTemplateRequest request)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();
            var template = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

            if (template == null)
                throw new BadHttpRequestException($"Template with ID '{id}' not found");

            if (template.TemplateName != request.TemplateName)
            {
                var exists = await repo.AnyAsync(t => t.TemplateName == request.TemplateName);
                if (exists)
                    throw new BadHttpRequestException($"Template name '{request.TemplateName}' already exists");
            }

            _mapper.Map(request, template);
            template.UpdateAt = DateTime.UtcNow;

            repo.UpdateAsync(template);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Updated email template: {TemplateName}", template.TemplateName);
            return _mapper.Map<EmailTemplateResponse>(template);
        }

        public async Task<bool> DeleteEmailTemplateAsync(Guid id)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();
            var template = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

            if (template == null) return false;

            repo.DeleteAsync(template);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Deleted email template: {TemplateName}", template.TemplateName);
            return true;
        }

        public async Task<string> RenderTemplateAsync(
           string templateName,
           string userEmail,
           string userName,
           string documentTitle,
           string documentVersion,
           DateTime? effectiveUntil,
           string documentLink,
           string dismissLink)
        {
            var template = await GetEmailTemplateByNameAsync(templateName);
            if (template == null)
                throw new BadHttpRequestException($"Template '{templateName}' not found");

            // ✅ FIX: Safe string replacement with null handling
            var content = template.BodyHtml
                .Replace("{{UserEmail}}", SanitizeValue(userEmail))
                .Replace("{{UserName}}", SanitizeValue(userName))
                .Replace("{{DocumentTitle}}", SanitizeValue(documentTitle))
                .Replace("{{DocumentVersion}}", SanitizeValue(documentVersion))
                .Replace("{{EffectiveUntil}}", effectiveUntil?.ToString("dd/MM/yyyy") ?? "N/A")
                .Replace("{{DocumentLink}}", SanitizeValue(documentLink))
                .Replace("{{DismissLink}}", SanitizeValue(dismissLink));

            return content;
        }

        // ✅ NEW: Enhanced template rendering for document workflow with more parameters
        public async Task<string> RenderDocumentWorkflowTemplateAsync(
            string templateName,
            string recipientEmail,
            string recipientName,
            string documentTitle,
            string documentVersion,
            string submitterName,
            string departmentName,
            DateTime submissionDate,
            string documentLink,
            string dismissLink,
            string? comments = null)
        {
            var template = await GetEmailTemplateByNameAsync(templateName);
            if (template == null)
                throw new BadHttpRequestException($"Template '{templateName}' not found");

            // ✅ FIX: Comprehensive template rendering with all workflow information
            var content = template.BodyHtml
                .Replace("{{RecipientEmail}}", SanitizeValue(recipientEmail))
                .Replace("{{RecipientName}}", SanitizeValue(recipientName))
                .Replace("{{UserEmail}}", SanitizeValue(recipientEmail))  // Backward compatibility
                .Replace("{{UserName}}", SanitizeValue(recipientName))    // Backward compatibility
                .Replace("{{DocumentTitle}}", SanitizeValue(documentTitle))
                .Replace("{{DocumentVersion}}", SanitizeValue(documentVersion))
                .Replace("{{SubmitterName}}", SanitizeValue(submitterName))
                .Replace("{{SubmittedBy}}", SanitizeValue(submitterName))  // Alternative placeholder
                .Replace("{{DepartmentName}}", SanitizeValue(departmentName))
                .Replace("{{SubmissionDate}}", submissionDate.ToString("dd/MM/yyyy HH:mm"))
                .Replace("{{SubmittedDate}}", submissionDate.ToString("dd/MM/yyyy"))  // Alternative format
                .Replace("{{DocumentLink}}", SanitizeValue(documentLink))
                .Replace("{{DismissLink}}", SanitizeValue(dismissLink))
                .Replace("{{Comments}}", SanitizeValue(comments) ?? "Không có ghi chú");

            _logger.LogDebug("Rendered template {TemplateName} for {RecipientEmail} with document {DocumentTitle}",
                templateName, recipientEmail, documentTitle);

            return content;
        }

        // ✅ NEW: Safe string sanitization
        private static string SanitizeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "[Không có thông tin]";

            // Basic HTML encoding for safety
            return value.Replace("<", "&lt;").Replace(">", "&gt;").Trim();
        }
    }
}
