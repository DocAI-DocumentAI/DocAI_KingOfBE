using Microsoft.AspNetCore.SignalR;
using Notification.API.Hubs;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;

namespace Notification.API.Services.Implement
{
    /// <summary>
    /// Service for handling document workflow notifications
    /// </summary>
    public class DocumentWorkflowNotificationService : IDocumentWorkflowNotificationService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly INotificationLogService _logService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ITemplateRendererUtil _templateRenderer;
        private readonly IAuthClient _authClient;
        private readonly ILogger<DocumentWorkflowNotificationService> _logger;
        private readonly IConfiguration _configuration;

        public DocumentWorkflowNotificationService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            INotificationLogService logService,
            IHubContext<NotificationHub> hubContext,
            ITemplateRendererUtil templateRenderer,
            IAuthClient authClient,
            ILogger<DocumentWorkflowNotificationService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logService = logService;
            _hubContext = hubContext;
            _templateRenderer = templateRenderer;
            _authClient = authClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendDocumentSubmissionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            UserInfo submitterInfo,
            string departmentId,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document submission notification for document {DocumentId} to department {DepartmentId}", 
                    documentId, departmentId);

                // Get department managers
                var managerEmails = await GetDepartmentManagerEmailsAsync(departmentId);
                if (!managerEmails.Any())
                {
                    _logger.LogWarning("No managers found for department {DepartmentId}", departmentId);
                    return;
                }

                // Get email template
                var templateResponse = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentSubmitted");
                if (templateResponse == null)
                {
                    _logger.LogError("Email template 'DocumentSubmitted' not found");
                    return;
                }

                // Prepare template data
                var templateData = new Dictionary<string, string>
                {
                    { "DocumentTitle", documentTitle },
                    { "DocumentVersion", documentVersion },
                    { "SubmittedBy", submitterInfo.FullName ?? submitterInfo.Email ?? "Unknown" },
                    { "DepartmentName", submitterInfo.DepartmentName ?? "Unknown Department" },
                    { "SubmissionDate", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm") },
                    { "DocumentLink", documentLink ?? "#" }
                };

                var subject = _templateRenderer.Render(templateResponse.Subject, templateData);
                var body = _templateRenderer.Render(templateResponse.BodyHtml, templateData);

                // Send notifications to each manager
                foreach (var managerEmail in managerEmails)
                {
                    await SendNotificationAsync(
                        managerEmail,
                        subject,
                        body,
                        NotificationType.DocumentSubmitted,
                        documentId,
                        documentVersion);
                }

                _logger.LogInformation("Document submission notifications sent to {Count} managers for document {DocumentId}", 
                    managerEmails.Count, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission notification for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserInfo approverInfo,
            string? comments = null,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document approval notification for document {DocumentId} to owner {OwnerEmail}", 
                    documentId, ownerEmail);

                // Get email template
                var templateResponse = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentApproved");
                if (templateResponse == null)
                {
                    _logger.LogError("Email template 'DocumentApproved' not found");
                    return;
                }

                // Prepare template data
                var templateData = new Dictionary<string, string>
                {
                    { "DocumentTitle", documentTitle },
                    { "DocumentVersion", documentVersion },
                    { "DocumentOwner", ownerName },
                    { "ApprovedBy", approverInfo.FullName ?? approverInfo.Email ?? "Unknown" },
                    { "ApprovalDate", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm") },
                    { "Comments", comments ?? "No additional comments" },
                    { "DocumentLink", documentLink ?? "#" }
                };

                var subject = _templateRenderer.Render(templateResponse.Subject, templateData);
                var body = _templateRenderer.Render(templateResponse.BodyHtml, templateData);

                await SendNotificationAsync(
                    ownerEmail,
                    subject,
                    body,
                    NotificationType.DocumentApproved,
                    documentId,
                    documentVersion);

                _logger.LogInformation("Document approval notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document approval notification for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task SendDocumentRejectionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserInfo reviewerInfo,
            string rejectionComments,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document rejection notification for document {DocumentId} to owner {OwnerEmail}", 
                    documentId, ownerEmail);

                // Get email template
                var templateResponse = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentRejected");
                if (templateResponse == null)
                {
                    _logger.LogError("Email template 'DocumentRejected' not found");
                    return;
                }

                // Prepare template data
                var templateData = new Dictionary<string, string>
                {
                    { "DocumentTitle", documentTitle },
                    { "DocumentVersion", documentVersion },
                    { "DocumentOwner", ownerName },
                    { "ReviewedBy", reviewerInfo.FullName ?? reviewerInfo.Email ?? "Unknown" },
                    { "ReviewDate", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm") },
                    { "Comments", rejectionComments },
                    { "DocumentLink", documentLink ?? "#" }
                };

                var subject = _templateRenderer.Render(templateResponse.Subject, templateData);
                var body = _templateRenderer.Render(templateResponse.BodyHtml, templateData);

                await SendNotificationAsync(
                    ownerEmail,
                    subject,
                    body,
                    NotificationType.DocumentRejected,
                    documentId,
                    documentVersion);

                _logger.LogInformation("Document rejection notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document rejection notification for document {DocumentId}", documentId);
                throw;
            }
        }

        /// <summary>
        /// Helper method to send both email and system notifications
        /// </summary>
        private async Task SendNotificationAsync(
            string recipientEmail,
            string subject,
            string body,
            NotificationType notificationType,
            string documentId,
            string documentVersion)
        {
            // Create dismiss token for email
            var dismissToken = Guid.NewGuid();
            var dismissLink = $"{_configuration["ApiBaseUrl"]}/api/notifications/dismiss-by-token?token={dismissToken}";

            // Send email notification
            bool emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, body);
            var emailLog = new NotificationLog
            {
                DocumentId = Guid.TryParse(documentId, out var docGuid) ? docGuid : Guid.Empty,
                DocumentVersion = documentVersion,
                NotificationType = notificationType,
                RecipientType = RecipientType.Email,
                RecipientAddress = recipientEmail,
                Subject = subject,
                Message = body,
                IsSent = emailSent,
                SentAt = emailSent ? DateTime.UtcNow : null,
                DismissToken = dismissToken
            };
            await _logService.CreateLogAsync(emailLog);

            // Send system notification via SignalR (if we can get user ID)
            try
            {
                var userInfo = await GetUserByEmailAsync(recipientEmail);
                if (userInfo != null)
                {
                    await _hubContext.Clients.User(userInfo.UserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        LogId = emailLog.Id,
                        Type = notificationType.ToString(),
                        Subject = subject,
                        Message = GetSystemNotificationMessage(notificationType, documentVersion),
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send system notification to {Email}", recipientEmail);
                // Don't fail the entire operation for system notification errors
            }
        }

        /// <summary>
        /// Get department manager emails from Auth service
        /// </summary>
        private async Task<List<string>> GetDepartmentManagerEmailsAsync(string departmentId)
        {
            try
            {
                return await _authClient.GetDepartmentManagerEmailsAsync(departmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department manager emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get user information by email from Auth service
        /// </summary>
        private async Task<UserDetailResponseExternal?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _authClient.GetUserByEmailAsync(email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get user info for email {Email}", email);
                return null;
            }
        }

        /// <summary>
        /// Generate system notification message based on notification type
        /// </summary>
        private static string GetSystemNotificationMessage(NotificationType type, string documentVersion)
        {
            return type switch
            {
                NotificationType.DocumentSubmitted => $"New document (version {documentVersion}) submitted for your review",
                NotificationType.DocumentApproved => $"Your document (version {documentVersion}) has been approved",
                NotificationType.DocumentRejected => $"Your document (version {documentVersion}) requires revision",
                _ => "Document workflow notification"
            };
        }
    }
}
