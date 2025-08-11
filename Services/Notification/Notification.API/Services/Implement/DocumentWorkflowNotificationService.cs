using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Notification.API.Hubs;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Shared.DTOs;

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
        private readonly IUserService _userService;
        private readonly ILogger<DocumentWorkflowNotificationService> _logger;

        public DocumentWorkflowNotificationService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            INotificationLogService logService,
            IHubContext<NotificationHub> hubContext,
            IUserService userService,
            ILogger<DocumentWorkflowNotificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logService = logService;
            _hubContext = hubContext;
            _userService = userService;
            _logger = logger;
        }

        public async Task SendDocumentSubmissionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            UserDto submitterInfo,
            Guid departmentId,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document submission notification for document {DocumentId} to department {DepartmentId}",
                    documentId, departmentId);

                var managers = await _userService.GetDepartmentManagersAsync(departmentId);
                if (!managers.Any())
                {
                    _logger.LogWarning("No managers found for department {DepartmentId}", departmentId);
                    return;
                }

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentSubmitted");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentSubmitted' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/documents/{documentId}";

                foreach (var manager in managers)
                {
                    await SendNotificationAsync(
                        manager.Email,
                        $"[{submitterInfo.DepartmentName}] Tài liệu '{documentTitle}' cần duyệt",
                        template.TemplateName,
                        documentTitle,
                        documentVersion,
                        DateTime.UtcNow,
                        finalDocumentLink,
                        manager.Name,
                        NotificationType.DocumentUpdate,
                        documentId,
                        documentVersion,
                        manager);
                }

                _logger.LogInformation("Document submission notifications sent to {Count} managers for document {DocumentId}",
                    managers.Count, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission notification for document {DocumentId}", documentId);
                throw;
            }
                try
                {
                    _logger.LogInformation("Sending document rejection notification for document {DocumentId} to owner {OwnerEmail}",
                        documentId, ownerEmail);

                    // Get email template
                    var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentRejected");
                    if (template == null)
                    {
                        _logger.LogError("Email template 'DocumentRejected' not found");
                        return;
                    }

                    var finalDocumentLink = documentLink ?? $"https://docai.asia/documents/{documentId}";

                    // Get owner user info for SignalR
                    var owner = await GetUserByEmailAsync(ownerEmail);

                    await SendNotificationAsync(
                        ownerEmail,
                        $"[{reviewerInfo.Department}] Tài liệu '{documentTitle}' cần chỉnh sửa",
                        template.TemplateName,
                        documentTitle,
                        documentVersion,
                        DateTime.UtcNow,
                        finalDocumentLink,
                        ownerName,
                        NotificationType.DocumentUpdate,
                        documentId,
                        documentVersion,
                        owner);

                    _logger.LogInformation("Document rejection notification sent for document {DocumentId}", documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending document rejection notification for document {DocumentId}", documentId);
                    throw;
                }
            }

            public async Task SendDocumentPublicationNotificationAsync(
                string documentId,
                string documentTitle,
                string documentVersion,
                UserInfo approverInfo,
                string departmentId,
                bool isPublic,
                string documentTypeId,
                string? documentTypeName = null,
                DateTime? effectiveFrom = null,
                DateTime? effectiveUntil = null,
                List<string>? tags = null,
                string? documentLink = null)
            {
                try
                {
                    _logger.LogInformation("Sending document publication notification for document {DocumentId} to department {DepartmentId}",
                        documentId, departmentId);

                    // Get email template
                    var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentPublished");
                    if (template == null)
                    {
                        _logger.LogError("Email template 'DocumentPublished' not found");
                        return;
                    }

                    var finalDocumentLink = documentLink ?? $"https://docai.asia/documents/{documentId}";

                    // Get department users (all users in the department)
                    var departmentUsers = await _userService.GetUsersByDepartmentAsync(Guid.Parse(departmentId));
                    if (!departmentUsers.Any())
                    {
                        _logger.LogWarning("No users found for department {DepartmentId}", departmentId);
                        return;
                    }

                    // If document is public, also notify users from other departments who have access
                    var allRecipients = new List<UserInfo>(departmentUsers);
                    if (isPublic)
                    {
                        // For public documents, we could add logic to notify other departments
                        // For now, we'll just notify the document's department
                        _logger.LogInformation("Document {DocumentId} is public, but currently only notifying department {DepartmentId}", documentId, departmentId);
                    }

                    // Send notifications to all recipients
                    var notificationTasks = allRecipients.Select(async user =>
                    {
                        try
                        {
                            var subject = $"[{approverInfo.Department}] Tài liệu mới '{documentTitle}' đã được phát hành";

                            await SendNotificationAsync(
                                user.Email,
                                subject,
                                template.TemplateName,
                                documentTitle,
                                documentVersion,
                                DateTime.UtcNow,
                                finalDocumentLink,
                                user.Name,
                                NotificationType.DocumentUpdate,
                                documentId,
                                documentVersion,
                                user);

                            _logger.LogDebug("Document publication notification sent to {UserEmail} for document {DocumentId}", user.Email, documentId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending document publication notification to {UserEmail} for document {DocumentId}", user.Email, documentId);
                        }
                    });

                    await Task.WhenAll(notificationTasks);

                    _logger.LogInformation("Document publication notification sent to {UserCount} users in department {DepartmentId} for document {DocumentId}",
                        allRecipients.Count, departmentId, documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending document publication notification for document {DocumentId}", documentId);
                    throw;
                }
            }

            /// <summary>
            /// Helper method to send both email and system notifications
            /// </summary>
            private async Task SendNotificationAsync(
                string recipientEmail,
                string subject,
                string templateName,
                string documentTitle,
                string documentVersion,
                DateTime effectiveDate,
                string documentLink,
                string recipientName,
                NotificationType notificationType,
                string documentId,
                string version,
                UserInfo? userInfo)
            {
                var dismissToken = Guid.NewGuid();
                var dismissLink = $"https://docai.asia/api/notification/dismiss-by-token?token={dismissToken}";

                // Render email content
                var emailBody = await _emailTemplateService.RenderTemplateAsync(
                    templateName,
                    recipientEmail,
                    recipientName,
                    documentTitle,
                    documentVersion,
                    effectiveDate,
                    documentLink,
                    dismissLink);

                var emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, emailBody);

                // Create notification log
                var log = new NotificationLog
                {
                    DocumentId = documentId,
                    DocumentVersion = version,
                    NotificationType = notificationType,
                    RecipientType = RecipientType.Email,
                    RecipientAddress = recipientEmail,
                    Subject = subject,
                    Message = emailBody,
                    IsSent = emailSent,
                    SentAt = emailSent ? DateTime.UtcNow : null,
                    DismissToken = dismissToken,
                    ErrorMessage = emailSent ? null : "Failed to send email notification"
                };

                await _logService.CreateLogAsync(log);

                // Send SignalR notification if we have user info
                if (userInfo != null)
                {
                    await SendSignalRNotificationAsync(
                        userInfo,
                        subject,
                        GetSystemNotificationMessage(notificationType, documentTitle),
                        Guid.Parse(documentId));
                }
            }

            private async Task SendSignalRNotificationAsync(
                UserInfo user,
                string subject,
                string message,
                Guid documentId)
            {
                try
                {
                    await _hubContext.Clients.User(user.UserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        Type = "DocumentWorkflow",
                        Subject = subject,
                        Message = message,
                        Timestamp = DateTime.UtcNow,
                        DocumentId = documentId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending SignalR notification to user {UserId}", user.UserId);
                }
            }

            private async Task<UserInfo?> GetUserByEmailAsync(string email)
            {
                try
                {
                    // Simple approach: get users from common roles and find by email
                    var allUsers = new List<UserInfo>();
                    var roles = new[] { "Admin", "Manager", "Editor", "Employee" };

                    foreach (var role in roles)
                    {
                        var roleUsers = await _userService.GetUsersByRoleAsync(role);
                        allUsers.AddRange(roleUsers);
                    }

                    // Convert UserInfo from UserService to Utils.UserInfo
                    var user = allUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                    if (user != null)
                    {
                        return new UserInfo
                        {
                            UserId = user.UserId,
                            Email = user.Email,
                            Name = user.Name,
                            Department = user.Department,
                            DepartmentId = "", // We don't have this info from UserService
                        };
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting user by email {Email}", email);
                    return null;
                }
            }

            /// <summary>
            /// Generate system notification message based on notification type
            /// </summary>
            private static string GetSystemNotificationMessage(NotificationType type, string documentTitle)
            {
                return type switch
                {
                    NotificationType.DocumentUpdate => $"Document workflow update for '{documentTitle}'",
                    _ => "Document workflow notification"
                };
            }
        

        public async Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserDto approverInfo,
            string? comments = null,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document approval notification for document {DocumentId} to owner {OwnerEmail}",
                    documentId, ownerEmail);

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentApproved");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentApproved' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/documents/{documentId}";
                var owner = await GetUserByEmailAsync(ownerEmail);

                await SendNotificationAsync(
                    ownerEmail,
                    $"[{approverInfo.DepartmentName}] Tài liệu '{documentTitle}' đã được duyệt",
                    template.TemplateName,
                    documentTitle,
                    documentVersion,
                    DateTime.UtcNow,
                    finalDocumentLink,
                    ownerName,
                    NotificationType.DocumentUpdate,
                    documentId,
                    documentVersion,
                    owner);

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
            UserDto reviewerInfo,
            string rejectionComments,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document rejection notification for document {DocumentId} to owner {OwnerEmail}",
                    documentId, ownerEmail);

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentRejected");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentRejected' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/documents/{documentId}";
                var owner = await GetUserByEmailAsync(ownerEmail);

                await SendNotificationAsync(
                    ownerEmail,
                    $"[{reviewerInfo.DepartmentName}] Tài liệu '{documentTitle}' cần chỉnh sửa",
                    template.TemplateName,
                    documentTitle,
                    documentVersion,
                    DateTime.UtcNow,
                    finalDocumentLink,
                    ownerName,
                    NotificationType.DocumentUpdate,
                    documentId,
                    documentVersion,
                    owner);

                _logger.LogInformation("Document rejection notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document rejection notification for document {DocumentId}", documentId);
                throw;
            }
        }

        private async Task SendNotificationAsync(
            string recipientEmail,
            string subject,
            string templateName,
            string documentTitle,
            string documentVersion,
            DateTime effectiveDate,
            string documentLink,
            string recipientName,
            NotificationType notificationType,
            string documentId,
            string version,
            UserDto? userInfo)
        {
            var dismissToken = Guid.NewGuid();
            var dismissLink = $"https://docai.asia/api/notification/dismiss-by-token?token={dismissToken}";

            var emailBody = await _emailTemplateService.RenderTemplateAsync(
                templateName,
                recipientEmail,
                recipientName,
                documentTitle,
                documentVersion,
                effectiveDate,
                documentLink,
                dismissLink);

            var emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, emailBody);

            var log = new NotificationLog
            {
                DocumentId = documentId,
                DocumentVersion = version,
                NotificationType = notificationType,
                RecipientType = RecipientType.Email,
                RecipientAddress = recipientEmail,
                Subject = subject,
                Message = emailBody,
                IsSent = emailSent,
                SentAt = emailSent ? DateTime.UtcNow : null,
                DismissToken = dismissToken,
                ErrorMessage = emailSent ? null : "Failed to send email notification"
            };

            await _logService.CreateLogAsync(log);

            if (userInfo != null)
            {
                await SendSignalRNotificationAsync(
                    userInfo,
                    subject,
                    GetSystemNotificationMessage(notificationType, documentTitle),
                    Guid.Parse(documentId));
            }
        }

        private async Task SendSignalRNotificationAsync(
            UserDto user,
            string subject,
            string message,
            Guid documentId)
        {
            try
            {
                await _hubContext.Clients.User(user.UserId.ToString()).SendAsync("ReceiveNotification", new
                {
                    Type = "DocumentWorkflow",
                    Subject = subject,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    DocumentId = documentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR notification to user {UserId}", user.UserId);
            }
        }

        private async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            try
            {
                var allUsers = new List<UserDto>();
                var roles = new[] { "Admin", "Manager", "Editor", "Employee" };

                foreach (var role in roles)
                {
                    var roleUsers = await _userService.GetUsersByRoleAsync(role);
                    allUsers.AddRange(roleUsers);
                }

                return allUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email}", email);
                return null;
            }
        }

        private static string GetSystemNotificationMessage(NotificationType type, string documentTitle)
        {
            return type switch
            {
                NotificationType.DocumentUpdate => $"Document workflow update for '{documentTitle}'",
                _ => "Document workflow notification"
            };
        }
    }
}

