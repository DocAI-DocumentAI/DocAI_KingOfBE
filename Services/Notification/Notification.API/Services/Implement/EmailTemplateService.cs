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
        private readonly TemplateRendererUtil _templateRenderer;
        public EmailTemplateService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IMapper mapper,
            TemplateRendererUtil templateRenderer,
            ILogger<EmailTemplateService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _templateRenderer = templateRenderer;
            _logger = logger;
        }

        public async Task<EmailTemplateResponse> CreateEmailTemplateAsync(EmailTemplateRequest request)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();
            var existingTemplate = await repo.SingleOrDefaultAsync(predicate: t => t.TemplateName == request.TemplateName);
            if (existingTemplate != null)
            {
                throw new BadHttpRequestException($"Email template with name '{request.TemplateName}' already exists.");
            }

            var emailTemplate = _mapper.Map<EmailTemplate>(request);
            await repo.InsertAsync(emailTemplate);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created new email template: {TemplateName}", emailTemplate.TemplateName);
            return _mapper.Map<EmailTemplateResponse>(emailTemplate);
        }


        public async Task<EmailTemplateResponse?> GetEmailTemplateByIdAsync(Guid id)
        {
            var emailTemplate = await _unitOfWork.GetRepository<EmailTemplate>()
                .SingleOrDefaultAsync(predicate: t => t.Id == id);

            if (emailTemplate == null)
            {
                _logger.LogWarning("Email template with ID '{Id}' not found.", id);
                return null;
            }

            return _mapper.Map<EmailTemplateResponse>(emailTemplate);
        }

        public async Task<EmailTemplateResponse?> GetEmailTemplateByNameAsync(string templateName)
        {
            var emailTemplate = await _unitOfWork.GetRepository<EmailTemplate>()
                .SingleOrDefaultAsync(predicate: t => t.TemplateName == templateName);

            if (emailTemplate == null)
            {
                _logger.LogWarning("Email template with name '{TemplateName}' not found.", templateName);
                return null;
            }

            return _mapper.Map<EmailTemplateResponse>(emailTemplate);
        }

        public async Task<IPaginate<EmailTemplateResponse>> GetAllEmailTemplatesAsync(
          int page,
          int size,
          EmailTemplateFilter? filter,
          string? sortBy,
          bool isAsc)
        {
            // Lấy dữ liệu từ repository với filter và sorting
            var templates = await _unitOfWork.GetRepository<EmailTemplate>().GetPagingListAsync(
                selector: t => new EmailTemplate
                {
                    Id = t.Id,
                    TemplateName = t.TemplateName,
                    Subject = t.Subject,
                    BodyHtml = t.BodyHtml,
                    AssociatedEvent = t.AssociatedEvent,
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt
                },
                page: page,
                size: size,
                filter: filter,
                sortBy: sortBy,
                isAsc: isAsc
            );

            var response = _mapper.Map<IPaginate<EmailTemplateResponse>>(templates);
            return response;
        }

        public async Task<EmailTemplateResponse> UpdateEmailTemplateAsync(Guid id, EmailTemplateRequest request)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();
            var emailTemplate = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

            if (emailTemplate == null)
            {
                _logger.LogWarning("Attempted to update a non-existent email template with ID: {Id}", id);
                throw new BadHttpRequestException($"Email template with ID '{id}' not found.");
            }

            // Check for name conflict if the name is being changed
            if (emailTemplate.TemplateName != request.TemplateName)
            {
                var existingTemplateWithNewName = await repo.SingleOrDefaultAsync(predicate: t => t.TemplateName == request.TemplateName);
                if (existingTemplateWithNewName != null)
                {
                    throw new BadHttpRequestException($"Cannot update template. Another template with name '{request.TemplateName}' already exists.");
                }
            }

            _mapper.Map(request, emailTemplate);
            emailTemplate.UpdateAt = DateTime.UtcNow;
              
            repo.UpdateAsync(emailTemplate);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully updated email template with ID: {Id}", id);
            return _mapper.Map<EmailTemplateResponse>(emailTemplate);
        }

        public async Task<bool> DeleteEmailTemplateAsync(Guid id)
        {
            var repo = _unitOfWork.GetRepository<EmailTemplate>();
            var emailTemplate = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

            if (emailTemplate == null)
            {
                _logger.LogWarning("Attempted to delete a non-existent email template with ID: {Id}", id);
                return false;
            }

            repo.DeleteAsync(emailTemplate);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully deleted email template with ID: {Id}", id);
            return true;
        }

        public async Task<string> PreviewEmailTemplateAsync(PreviewEmailTemplateRequest request)
        {
            var emailTemplate = await GetEmailTemplateByNameAsync(request.TemplateName);
            if (emailTemplate == null)
            {
                throw new BadHttpRequestException($"Email template with name '{request.TemplateName}' not found for preview.");
            }

            return _templateRenderer.Render(emailTemplate.BodyHtml, request.Data);
        }
    }
}
