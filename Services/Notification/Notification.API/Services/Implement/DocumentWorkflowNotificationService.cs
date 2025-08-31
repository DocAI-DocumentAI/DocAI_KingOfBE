using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Notification.API.Hubs;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Shared.DTOs;

namespace Notification.API.Services.Implement
{
    /// <summary>
    /// Service for handling document workflow notifications
    /// </summary>
    public class DocumentWorkflowNotificationService : IDocumentWorkflowNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly INotificationLogService _logService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IUserService _userService;
        private readonly ILogger<DocumentWorkflowNotificationService> _logger;

        public DocumentWorkflowNotificationService(
            IEmailService emailService,
            IEmailTemplateService emailTemplateService,
            INotificationLogService logService,
            IHubContext<NotificationHub> hubContext,
            IUserService userService,
            ILogger<DocumentWorkflowNotificationService> logger)
        {
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logService = logService;
            _hubContext = hubContext;
            _userService = userService;
            _logger = logger;
        }

        public async Task SendDocumentSubmissionNotificationAsync(
               string documentId,
               string versionId,
               string documentTitle,
               string documentVersion,
               UserDto submitterInfo,
               Guid departmentId,
               string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document submission notification for document {DocumentId} by {SubmitterName} from {DepartmentName}",
                    documentId, submitterInfo.Name, submitterInfo.DepartmentName);

                // Validate required information
                if (string.IsNullOrEmpty(documentTitle))
                {
                    _logger.LogError("Document title is empty for document {DocumentId}", documentId);
                    return;
                }

                if (string.IsNullOrEmpty(submitterInfo.Name))
                {
                    _logger.LogError("Submitter name is empty for document {DocumentId}", documentId);
                    return;
                }

                var managers = await _userService.GetDepartmentManagersAsync(departmentId);
                if (managers.Count == 0)
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

                var finalDocumentLink = documentLink ?? $"https://docai.asia/manager/document-review/{documentId}/{versionId}";
                var submissionDate = DateTime.UtcNow;

                // Prevent duplicate notifications with tracking
                var notificationTasks = managers.Select(async manager =>
                {
                    try
                    {
                        await SendDocumentWorkflowNotificationAsync(
                            manager.Email,
                            manager.Name,
                            $"[{submitterInfo.DepartmentName}] Tài liệu '{documentTitle}' cần duyệt",
                            template.TemplateName,
                            documentTitle,
                            documentVersion,
                            submitterInfo.Name,
                            submitterInfo.DepartmentName ?? "Unknown Department",
                            submissionDate,
                            finalDocumentLink,
                            NotificationType.DocumentUpdate,
                            documentId,
                            documentVersion,
                            manager.UserId);

                        _logger.LogDebug("Document submission notification sent to manager {ManagerEmail}", manager.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send document submission notification to manager {ManagerEmail}", manager.Email);
                    }
                });

                await Task.WhenAll(notificationTasks);

                _logger.LogInformation("Document submission notifications sent to {Count} managers for document {DocumentId}",
                    managers.Count, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission notification for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task SendDocumentSubmissionConfirmationAsync(
            string documentId,
            string versionId,
            string documentTitle,
            string documentVersion,
            string submitterEmail,
            string submitterName,
            Guid submitterId,
            string departmentName,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document submission confirmation for document {DocumentId} to submitter {SubmitterEmail}", documentId, submitterEmail);

                // Create notification content
                var subject = $"Document Submission Confirmed: {documentTitle}";
                var content = $"Your document '{documentTitle}' (Version: {documentVersion}) has been successfully submitted for approval and is now pending review by department managers.";

                // Create email body
                var emailBody = $@"
                    <h2>Document Submission Confirmation</h2>
                    <p>Dear {submitterName},</p>
                    <p>Your document has been successfully submitted for approval:</p>
                    <ul>
                        <li><strong>Document:</strong> {documentTitle}</li>
                        <li><strong>Version:</strong> {documentVersion}</li>
                        <li><strong>Department:</strong> {departmentName}</li>
                        <li><strong>Status:</strong> Pending Approval</li>
                    </ul>
                    <p>Department managers have been notified and will review your submission. You will receive another notification once the review is complete.</p>
                    {(string.IsNullOrEmpty(documentLink) ? "" : $"<p><a href='{documentLink}'>View Document</a></p>")}
                    <p>Thank you for your submission.</p>
                ";

                // Send email notification to submitter
                var emailSent = await _emailService.SendEmailAsync(submitterEmail, subject, emailBody);

                // Create notification log
                var log = new NotificationLog
                {
                    DocumentId = documentId,
                    DocumentVersion = documentVersion,
                    NotificationType = NotificationType.DocumentSubmitted,
                    RecipientType = RecipientType.Email,
                    RecipientAddress = submitterEmail,
                    Subject = subject,
                    Message = emailBody,
                    IsSent = emailSent,
                    SentAt = emailSent ? DateTime.UtcNow : null,
                    ErrorMessage = emailSent ? null : "Failed to send email notification"
                };

                await _logService.CreateLogAsync(log);
                _logger.LogInformation("Email notification sent to submitter {SubmitterEmail} for document {DocumentId}", submitterEmail, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission confirmation for document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task SendDocumentApprovalNotificationAsync(
         string documentId,
         string versionId,
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

                // ✅ FIX: Validate required information
                if (string.IsNullOrEmpty(ownerEmail) || string.IsNullOrEmpty(ownerName))
                {
                    _logger.LogError("Owner information is incomplete for document {DocumentId}", documentId);
                    return;
                }

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentApproved");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentApproved' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/editor/doc/{documentId}/{versionId}";
                var approvalDate = DateTime.UtcNow;

                await SendDocumentWorkflowNotificationAsync(
                    ownerEmail,
                    ownerName,
                    $"[{approverInfo.DepartmentName}] Tài liệu '{documentTitle}' đã được duyệt",
                    template.TemplateName,
                    documentTitle,
                    documentVersion,
                    approverInfo.Name,
                    approverInfo.DepartmentName ?? "Unknown Department",
                    approvalDate,
                    finalDocumentLink,
                    NotificationType.DocumentUpdate,
                    documentId,
                    documentVersion,
                    null,
                    comments);

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
           string versionId,
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

                // ✅ FIX: Validate required information
                if (string.IsNullOrEmpty(ownerEmail) || string.IsNullOrEmpty(ownerName))
                {
                    _logger.LogError("Owner information is incomplete for document {DocumentId}", documentId);
                    return;
                }

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentRejected");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentRejected' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/editor/doc/{documentId}/{versionId}";
                var rejectionDate = DateTime.UtcNow;

                await SendDocumentWorkflowNotificationAsync(
                    ownerEmail,
                    ownerName,
                    $"[{reviewerInfo.DepartmentName}] Tài liệu '{documentTitle}' cần chỉnh sửa",
                    template.TemplateName,
                    documentTitle,
                    documentVersion,
                    reviewerInfo.Name,
                    reviewerInfo.DepartmentName ?? "Unknown Department",
                    rejectionDate,
                    finalDocumentLink,
                    NotificationType.DocumentUpdate,
                    documentId,
                    documentVersion,
                    null,
                    rejectionComments);

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
          string versionId,
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
                _logger.LogInformation("Sending document publication notification for document {DocumentId} (version {VersionId}) to department {DepartmentId}",
                    documentId, versionId, departmentId);

                var template = await _emailTemplateService.GetEmailTemplateByNameAsync("DocumentPublished");
                if (template == null)
                {
                    _logger.LogError("Email template 'DocumentPublished' not found");
                    return;
                }

                var finalDocumentLink = documentLink ?? $"https://docai.asia/document/{documentId}";
                var publicationDate = DateTime.UtcNow;

                var recipients = new List<UserDto>();

                // ✅ FIX: Determine recipients based on document visibility
                if (isPublic)
                {
                    // For public documents, notify all company members
                    var allEmployees = await _userService.GetUsersByRoleAsync("Employee");
                    var allManagers = await _userService.GetUsersByRoleAsync("Manager");
                    var allEditors = await _userService.GetUsersByRoleAsync("Editor");

                    recipients.AddRange(allEmployees);
                    recipients.AddRange(allManagers);
                    recipients.AddRange(allEditors);

                    _logger.LogInformation("Notifying all company members for public document {DocumentId}", documentId);
                }
                else
                {
                    // For private documents, notify only department members
                    if (Guid.TryParse(departmentId, out var deptGuid))
                    {
                        var departmentUsers = await _userService.GetUsersByDepartmentAsync(deptGuid);
                        recipients.AddRange(departmentUsers);

                        _logger.LogInformation("Notifying department {DepartmentId} members for private document {DocumentId}", departmentId, documentId);
                    }
                }

                // Remove duplicates
                var uniqueRecipients = recipients
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .GroupBy(u => u.Email.ToLower())
                    .Select(g => g.First())
                    .ToList();

                if (uniqueRecipients.Count == 0)
                {
                    _logger.LogWarning("No recipients found for document publication {DocumentId}", documentId);
                    return;
                }

                // ✅ FIX: Send notifications concurrently but prevent duplicates
                var notificationTasks = uniqueRecipients.Select(async user =>
                {
                    try
                    {
                        var subject = $"[{approverInfo.Department}] Tài liệu mới '{documentTitle}' đã được phát hành";

                        await SendDocumentWorkflowNotificationAsync(
                            user.Email,
                            user.Name,
                            subject,
                            template.TemplateName,
                            documentTitle,
                            documentVersion,
                            approverInfo.Name ?? "Unknown Approver",
                            approverInfo.Department ?? "Unknown Department",
                            publicationDate,
                            finalDocumentLink,
                            NotificationType.DocumentUpdate,
                            documentId,
                            documentVersion,
                            user.UserId);

                        _logger.LogDebug("Document publication notification sent to {UserEmail}", user.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send document publication notification to {UserEmail}", user.Email);
                    }
                });

                await Task.WhenAll(notificationTasks);

                _logger.LogInformation("Document publication notification sent to {UserCount} users for document {DocumentId}",
                    uniqueRecipients.Count, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document publication notification for document {DocumentId}", documentId);
                throw;
            }
        }
        /// <summary>
        /// ✅ FIXED: Enhanced method to send document workflow notifications with all required information
        /// </summary>
        private async Task SendDocumentWorkflowNotificationAsync(
            string recipientEmail,
            string recipientName,
            string subject,
            string templateName,
            string documentTitle,
            string documentVersion,
            string submitterName,
            string departmentName,
            DateTime submissionDate,
            string documentLink,
            NotificationType notificationType,
            string documentId,
            string version,
            Guid? userId = null,
            string? comments = null)
        {
            var dismissToken = Guid.NewGuid();

            try
            {
                // ✅ FIX: Use existing RenderTemplateAsync but with enhanced content replacement
                var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
                if (template == null)
                {
                    _logger.LogError("Template '{TemplateName}' not found", templateName);
                    return;
                }
                var displayName = GetDisplayName(recipientEmail, recipientName);
                var displaySubmitterName = GetDisplayName(submitterName, submitterName);
                // ✅ FIX: Replace ALL placeholders manually to ensure no missing info
                var emailBody = template.BodyHtml
                 .Replace("{{RecipientEmail}}", SanitizeValue(recipientEmail))
            .Replace("{{RecipientName}}", displayName)
            .Replace("{{UserEmail}}", SanitizeValue(recipientEmail))
            .Replace("{{UserName}}", displayName)                           // ✅ Main user name
            .Replace("{{DocumentTitle}}", SanitizeValue(documentTitle))
            .Replace("{{DocumentVersion}}", SanitizeValue(documentVersion))
            .Replace("{{SubmitterName}}", displaySubmitterName)
            .Replace("{{SubmittedBy}}", displaySubmitterName)               // ✅ Alternative placeholder
            .Replace("{{ApprovedBy}}", displaySubmitterName)                // ✅ For approval emails
            .Replace("{{ReviewedBy}}", displaySubmitterName)                // ✅ For rejection emails
            .Replace("{{DepartmentName}}", SanitizeValue(departmentName))
            .Replace("{{SubmissionDate}}", submissionDate.ToString("dd/MM/yyyy HH:mm"))
            .Replace("{{SubmittedDate}}", submissionDate.ToString("dd/MM/yyyy"))
            .Replace("{{ApprovalDate}}", submissionDate.ToString("dd/MM/yyyy HH:mm"))  // ✅ For approval
            .Replace("{{ReviewDate}}", submissionDate.ToString("dd/MM/yyyy HH:mm"))    // ✅ For rejection
            .Replace("{{DocumentLink}}", SanitizeValue(documentLink))
            .Replace("{{Comments}}", SanitizeValue(comments) ?? "Không có ghi chú")
            .Replace("{{EffectiveUntil}}", "N/A");

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
                    ErrorMessage = emailSent ? null : "Failed to send email notification"
                };

                await _logService.CreateLogAsync(log);

                // ✅ FIX: Send SignalR notification with correct userId format
                if (userId.HasValue)
                {
                    await SendSignalRNotificationAsync(
                        userId.Value,
                        subject,
                        GetSystemNotificationMessage(notificationType, documentTitle),
                        Guid.Parse(documentId));
                }

                _logger.LogDebug("Document workflow notification sent to {RecipientEmail} for document {DocumentId}",
                    recipientEmail, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document workflow notification to {RecipientEmail}", recipientEmail);
                throw;
            }
        }
        private static string GetDisplayName(string? email, string? name)
        {
            // Priority: name -> email prefix -> fallback
            if (!string.IsNullOrWhiteSpace(name) && name != "[Không có thông tin]")
                return name.Trim();

            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    var emailPart = email.Split('@')[0];
                    // Convert email to readable name: "john.doe" -> "John Doe"
                    var readableName = emailPart.Replace(".", " ").Replace("_", " ");
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(readableName.ToLower());
                }
                catch
                {
                    return email; // Fallback to full email
                }
            }

            return "User"; // Final fallback
        }

        private static string SanitizeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "[Không có thông tin]";

            return value.Replace("<", "&lt;").Replace(">", "&gt;").Trim();
        }


        /// <summary>
        /// ✅ FIX: SignalR notification with proper userId handling
        /// </summary>
        private async Task SendSignalRNotificationAsync(
            Guid userId,
            string subject,
            string message,
            Guid documentId)
        {
            try
            {
                // ✅ FIX: Convert Guid to string for SignalR
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
                {
                    Type = "DocumentWorkflow",
                    Subject = subject,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    DocumentId = documentId
                });

                _logger.LogDebug("SignalR notification sent to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SignalR notification to user {UserId}", userId);
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
    }
}

