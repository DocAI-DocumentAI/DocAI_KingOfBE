using System.Net.Mail;
using System.Net;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Notification.API.Hubs;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Logging;
using Notification.Domain.Enums;
using Notification.API.Utils;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;
using Shared.Command;
using Shared.Enum;
using Shared.Models;
using MassTransit;
using Shared.DTOs;
using Quartz.Impl.AdoJobStore;
using Notification.API.Constants;

namespace Notification.API.Services.Implement;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
    private readonly ILogger<NotificationService> _logger;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationLogService _logService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;

    public NotificationService(
        IUnitOfWork<NotificationDbContext> unitOfWork,
        ILogger<NotificationService> logger,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        INotificationLogService logService,
        IHubContext<NotificationHub> hubContext,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IUserService userService,
        IAuthorizationService authorizationService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _logService = logService;
        _hubContext = hubContext;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _userService = userService;
        _authorizationService = authorizationService;
    }

    public async Task ProcessNearingExpirationNotification(DocumentExpirationDto document)
    {
        await ProcessDocumentNotificationAsync(document, NotificationType.NearingExpiration,
            ApiConstants.DOCUMENT_NEARING_EXPIRATION_TEMPLATE);
    }

    public async Task ProcessExpiredDocumentNotification(DocumentExpirationDto document)
    {
        await ProcessDocumentNotificationAsync(document, NotificationType.Expired,
            ApiConstants.DOCUMENT_EXPIRED_TEMPLATE);

        await TryUpdateDocumentStatusAsync(document);
    }

    private async Task ProcessDocumentNotificationAsync(DocumentExpirationDto document,
        NotificationType type, string templateName)
    {
        try
        {
            _logger.LogInformation("Processing {Type} notification for document {DocId}/{Version}",
                type, document.DocumentId, document.Version);

            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogWarning("Template '{TemplateName}' not found", templateName);
                return;
            }

            var recipients = await GetAuthorizedRecipientsAsync(document);
            if (!recipients.Any())
            {
                _logger.LogWarning("No authorized recipients found for document {DocId}", document.DocumentId);
                return;
            }

            await SendNotificationsToRecipientsAsync(document, type, template.TemplateName, recipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Type} notification for {DocId}/{Version}",
                type, document.DocumentId, document.Version);
        }
    }

    private async Task<List<Payload.Response.UserInfo>> GetAuthorizedRecipientsAsync(DocumentExpirationDto document)
    {
        var recipients = new List<Payload.Response.UserInfo>();

        try
        {
            // Rule 1: Get department managers for the document's department
            var departmentManagers = await _userService.GetDepartmentManagersAsync(document.DepartmentId);
            recipients.AddRange(departmentManagers);

            // Rule 2: Get department editors for the document's department  
            var departmentEditors = await _userService.GetDepartmentEditorsAsync(document.DepartmentId);
            recipients.AddRange(departmentEditors);

            // Rule 3: Get document-specific stakeholders (if any)
            var documentStakeholders = await _userService.GetDocumentStakeholdersAsync(document.DocumentId);
            recipients.AddRange(documentStakeholders);

            // Rule 4: If document is public, notify admins
            if (document.IsPublic)
            {
                var admins = await _userService.GetUsersByRoleAsync("Admin");
                recipients.AddRange(admins);
            }

            // Rule 5: Always notify document creator if available
            if (!string.IsNullOrEmpty(document.CreatedBy) && Guid.TryParse(document.CreatedBy, out var creatorId))
            {
                var creator = await _userService.GetUserByIdAsync(creatorId);
                if (creator != null)
                {
                    recipients.Add(creator);
                }
            }

            // Remove duplicates by email
            var uniqueRecipients = recipients
                .Where(r => !string.IsNullOrEmpty(r.Email))
                .GroupBy(r => r.Email.ToLower())
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Found {Count} authorized recipients for document {DocId} in department {DeptName}",
                uniqueRecipients.Count, document.DocumentId, document.DepartmentName);

            return uniqueRecipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorized recipients for document {DocId}", document.DocumentId);
            return new List<Payload.Response.UserInfo>();
        }
    }

    private async Task SendNotificationsToRecipientsAsync(DocumentExpirationDto document,
        NotificationType type, string templateName, List<Payload.Response.UserInfo> recipients)
    {
        foreach (var user in recipients)
        {
            try
            {
                // Check if user has access to this document
                if (!HasDocumentAccess(user, document))
                {
                    _logger.LogDebug("User {Email} does not have access to document {DocId}, skipping notification",
                        user.Email, document.DocumentId);
                    continue;
                }

                await SendSingleNotificationAsync(document, type, templateName, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to {Email}", user.Email);
            }
        }
    }

    private bool HasDocumentAccess(Payload.Response.UserInfo user, DocumentExpirationDto document)
    {
        try
        {
            // Public documents can be accessed by anyone
            if (document.IsPublic) return true;

            // Check if user is in the same department
            if (user.Department.Equals(document.DepartmentName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Admin and Manager roles can access cross-department documents
            // Note: In real implementation, you'd get user's role from User service
            // For now, we'll allow all recipients from our authorized list
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking document access for user {Email}", user.Email);
            return false;
        }
    }

    private async Task SendSingleNotificationAsync(DocumentExpirationDto document,
        NotificationType type, string templateName, Payload.Response.UserInfo user)
    {
        var dismissToken = Guid.NewGuid();
        var dismissLink = $"https://docai.asia/api/notifications/dismiss-by-token?token={dismissToken}";
        var documentLink = $"https://docai.asia/documents/{document.DocumentId}";

        var emailBody = await _emailTemplateService.RenderTemplateAsync(
            templateName, user.Email, user.Name, document.Title, document.Version,
            document.EffectiveUntil, documentLink, dismissLink);

        var subject = type == NotificationType.Expired
            ? $"[{document.DepartmentName}] Tài liệu '{document.Title}' đã hết hạn"
            : $"[{document.DepartmentName}] Tài liệu '{document.Title}' sắp hết hạn";

        var emailSent = await _emailService.SendEmailAsync(user.Email, subject, emailBody);

        var log = new NotificationLog
        {
            DocumentId = document.DocumentId,
            DocumentVersion = document.Version,
            NotificationType = type,
            RecipientType = RecipientType.Email,
            RecipientAddress = user.Email,
            Subject = subject,
            Message = emailBody,
            IsSent = emailSent,
            SentAt = emailSent ? DateTime.UtcNow : null,
            DismissToken = dismissToken,
            ErrorMessage = emailSent ? null : "Failed to send email notification"
        };

        await _logService.CreateLogAsync(log);

        // Send SignalR notification
        await SendSignalRNotificationAsync(user, type, subject, document);

        _logger.LogInformation("Sent {Type} notification to {Email} for document {DocId}",
            type, user.Email, document.DocumentId);
    }

    private async Task SendSignalRNotificationAsync(Payload.Response.UserInfo user, NotificationType type,
        string subject, DocumentExpirationDto document)
    {
        try
        {
            await _hubContext.Clients.User(user.UserId.ToString()).SendAsync("ReceiveNotification", new
            {
                Type = type.ToString(),
                Subject = subject,
                Message = $"Tài liệu '{document.Title}' {(type == NotificationType.Expired ? "đã hết hạn" : "sắp hết hạn")}",
                Timestamp = DateTime.UtcNow,
                DocumentId = document.DocumentId,
                DepartmentName = document.DepartmentName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification to user {UserId}", user.UserId);
        }
    }

    private async Task TryUpdateDocumentStatusAsync(DocumentExpirationDto document)
    {
        try
        {
            var updateClient = _serviceProvider.GetService<IRequestClient<UpdateDocumentStatusCommand>>();
            if (updateClient == null) return;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await updateClient.GetResponse<UpdateDocumentStatusResponse>(
                new UpdateDocumentStatusCommand
                {
                    DocumentId = document.DocumentId,
                    Version = document.Version,
                    NewStatus = "Expired"
                },
                timeout.Token
            );

            _logger.LogInformation("Updated document {DocId} status to Expired", document.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status for {DocId}/{Version}",
                document.DocumentId, document.Version);
        }
    }

    public async Task<bool> DismissNotificationByUserAsync(Guid logId, Guid userId)
    {
        return await ProcessDismissalAsync(logId, userId, null);
    }

    public async Task<string> DismissNotificationByTokenAsync(Guid token)
    {
        var success = await ProcessDismissalAsync(null, null, token);
        return success ? "Notification dismissed successfully" : "Invalid or expired dismissal link";
    }

    private async Task<bool> ProcessDismissalAsync(Guid? logId, Guid? userId, Guid? token)
    {
        try
        {
            var logRepo = _unitOfWork.GetRepository<NotificationLog>();
            NotificationLog? log = null;

            if (logId.HasValue)
            {
                log = await logRepo.SingleOrDefaultAsync(predicate: l => l.Id == logId.Value);
            }
            else if (token.HasValue)
            {
                log = await logRepo.SingleOrDefaultAsync(predicate: l => l.DismissToken == token.Value);
            }

            if (log == null || log.IsDismissed) return false;

            log.IsDismissed = true;
            log.DismissedAt = DateTime.UtcNow;
            log.DismissedByUserId = userId;
            log.DismissToken = null;
            log.UpdateAt = DateTime.UtcNow;

            logRepo.UpdateAsync(log);
            await _unitOfWork.CommitAsync();

            await TryDeactivateDocumentWarningsAsync(log);

            _logger.LogInformation("Notification dismissed for document {DocId}/{Version}",
                log.DocumentId, log.DocumentVersion);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dismissal");
            return false;
        }
    }

    private async Task TryDeactivateDocumentWarningsAsync(NotificationLog log)
    {
        try
        {
            if (string.IsNullOrEmpty(log.DocumentVersion)) return;

            var deactivateClient = _serviceProvider.GetService<IRequestClient<DeactivateDocumentWarningsCommand>>();
            if (deactivateClient == null) return;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await deactivateClient.GetResponse<DeactivateDocumentWarningsResponse>(
                new DeactivateDocumentWarningsCommand
                {
                    DocumentId = log.DocumentId,
                    Version = log.DocumentVersion
                },
                timeout.Token
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deactivating document warnings");
        }
    }

    public async Task SendGeneralNotificationAsync(string templateName, string recipientEmail, string recipientName)
    {
        try
        {
            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogWarning("Template '{TemplateName}' not found", templateName);
                return;
            }

            var emailBody = await _emailTemplateService.RenderTemplateAsync(
                templateName, recipientEmail, recipientName, "", "", null, "", "");

            var emailSent = await _emailService.SendEmailAsync(recipientEmail, template.Subject, emailBody);

            var log = new NotificationLog
            {
                DocumentId = Guid.Empty,
                DocumentVersion = null,
                NotificationType = NotificationType.General,
                RecipientType = RecipientType.Email,
                RecipientAddress = recipientEmail,
                Subject = template.Subject,
                Message = emailBody,
                IsSent = emailSent,
                SentAt = emailSent ? DateTime.UtcNow : null
            };

            await _logService.CreateLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending general notification");
        }
    }
}