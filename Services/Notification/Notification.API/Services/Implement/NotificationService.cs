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
using System.Collections.Generic;

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

    public NotificationService(
        IUnitOfWork<NotificationDbContext> unitOfWork,
        ILogger<NotificationService> logger,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        INotificationLogService logService,
        IHubContext<NotificationHub> hubContext,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IUserService userService)
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
    }
    private async Task<List<UserDto>> GetAuthorizedRecipientsAsync(DocumentExpirationDto document)
    {
        var recipients = new List<UserDto>();

        try
        {
            // Get department managers
            var departmentManagers = await _userService.GetDepartmentManagersAsync(document.DepartmentId);
            recipients.AddRange(departmentManagers);

            // Get department editors
            var departmentEditors = await _userService.GetDepartmentEditorsAsync(document.DepartmentId);
            recipients.AddRange(departmentEditors);

            //// Get document stakeholders
            //var documentStakeholders = await _userService.GetDocumentStakeholdersAsync(document.DocumentId);
            //recipients.AddRange(documentStakeholders);

            //// If public, notify admins
            //if (document.IsPublic)
            //{
            //    var admins = await _userService.GetUsersByRoleAsync("Admin");
            //    recipients.AddRange(admins);
            //}

            // Always notify creator
            if (!string.IsNullOrEmpty(document.CreatedBy) && Guid.TryParse(document.CreatedBy, out var creatorId))
            {
                var creator = await _userService.GetUserByIdAsync(creatorId);
                if (creator != null)
                {
                    recipients.Add(creator);
                }
            }

            // Remove duplicates
            var uniqueRecipients = recipients
                .Where(r => !string.IsNullOrEmpty(r.Email))
                .GroupBy(r => r.Email.ToLower())
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Found {Count} authorized recipients for document {DocId}",
                uniqueRecipients.Count, document.DocumentId);

            return uniqueRecipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorized recipients for document {DocId}", document.DocumentId);
            return new List<UserDto>();
        }
    }
    private bool HasDocumentAccess(UserDto user, DocumentExpirationDto document)
    {
        try
        {
            if (document.IsPublic) return true;
            if (user.DepartmentName.Equals(document.DepartmentName, StringComparison.OrdinalIgnoreCase))
                return true;
            return true; // For now, allow all authorized recipients
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking document access for user {Email}", user.Email);
            return false;
        }
    }

    private string GetDaysUntilExpiration(DateTime? effectiveUntil)
    {
        if (!effectiveUntil.HasValue)
            return "N/A";

        // ✅ Sử dụng ngày Việt Nam để tính toán
        var vietnamDate = VietnamTimeHelper.GetVietnamDate();
        var days = (effectiveUntil.Value.Date - vietnamDate).Days;

        if (days < 0)
            return $"Đã hết hạn {Math.Abs(days)} ngày";
        else if (days == 0)
            return "Hết hạn hôm nay";
        else
            return $"Còn {days} ngày";
    }
    private async Task SendSignalRNotificationAsync(UserDto user, NotificationType type,
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
                DocumentId = null,
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
    public async Task ProcessNearingExpirationNotification(DocumentExpirationDto document)
    {
        await ProcessDocumentNotificationAsync(document, NotificationType.NearingExpiration,
            ApiConstants.DOCUMENT_NEARING_EXPIRATION_TEMPLATE);
    }

    public async Task ProcessExpiredDocumentNotification(DocumentExpirationDto document)
    {
        await ProcessDocumentNotificationAsync(document, NotificationType.Expired,
            ApiConstants.DOCUMENT_EXPIRED_TEMPLATE);

        // ✅ Try to update document status to Archived
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

    private async Task SendNotificationsToRecipientsAsync(DocumentExpirationDto document,
        NotificationType type, string templateName, List<UserDto> recipients)
    {
        foreach (var user in recipients)
        {
            try
            {
                if (!HasDocumentAccess(user, document))
                {
                    _logger.LogDebug("User {Email} does not have access to document {DocId}, skipping",
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

    private async Task SendSingleNotificationAsync(DocumentExpirationDto document,
        NotificationType type, string templateName, UserDto user)
    {
        try
        {
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime();
            _logger.LogInformation("Sending {Type} notification to {Email} for document {DocId}/{Version} at Vietnam time {VietnamTime}",
                type, user.Email, document.DocumentId, document.Version, vietnamTime);

            // ✅ SIMPLIFIED: Check duplicate only in last hour for same recipient
            var logRepo = _unitOfWork.GetRepository<NotificationLog>();

            var checkPeriod = type == NotificationType.Expired
                ? DateTime.UtcNow.AddHours(-24)
                : DateTime.UtcNow.AddHours(-24);

            var alreadySent = await logRepo.AnyAsync(l =>
                l.DocumentId == document.DocumentId &&
                l.DocumentVersion == document.Version &&
                l.NotificationType == type &&
                l.RecipientAddress == user.Email &&
                l.IsSent == true &&
                l.SentAt >= checkPeriod);

            if (alreadySent)
            {
                _logger.LogDebug("Notification already sent to {Email} for document {DocId}/{Version} in last hour",
                    user.Email, document.DocumentId, document.Version);
                return;
            }

            var documentLink = $"https://docai.asia/document/{document.DocumentId}";

            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogWarning("Template '{TemplateName}' not found", templateName);
                return;
            }

            // ✅ Sử dụng VietnamTimeHelper cho expiration status
            var expirationStatus = type == NotificationType.Expired ? "đã hết hạn" : "sắp hết hạn";
            var daysUntilExpiration = GetDaysUntilExpiration(document.EffectiveUntil);

            var emailBody = template.BodyHtml
                .Replace("{{RecipientEmail}}", user.Email ?? "")
                .Replace("{{RecipientName}}", user.Name ?? "")
                .Replace("{{UserEmail}}", user.Email ?? "")
                .Replace("{{UserName}}", user.Name ?? "")
                .Replace("{{DocumentTitle}}", document.Title ?? "")
                .Replace("{{DocumentVersion}}", document.Version ?? "")
                .Replace("{{EffectiveUntil}}", document.EffectiveUntil?.ToString("dd/MM/yyyy") ?? "N/A")
                .Replace("{{DocumentLink}}", documentLink)
                .Replace("{{DepartmentName}}", document.DepartmentName ?? "Unknown Department")
                .Replace("{{ExpirationStatus}}", expirationStatus)
                .Replace("{{DaysUntilExpiration}}", daysUntilExpiration)
                .Replace("{{VietnamTime}}", vietnamTime.ToString("dd/MM/yyyy HH:mm")); // ✅ Thêm Vietnam time

            var subject = type == NotificationType.Expired
                ? $"[{document.DepartmentName}] Tài liệu '{document.Title}' đã hết hạn"
                : $"[{document.DepartmentName}] Tài liệu '{document.Title}' sắp hết hạn";

            var emailSent = await _emailService.SendEmailAsync(user.Email, subject, emailBody);

            // ✅ SIMPLIFIED: Log without dismiss fields
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
                SentAt = emailSent ? vietnamTime : null,
                ErrorMessage = emailSent ? null : "Failed to send email notification"
            };

            await _logService.CreateLogAsync(log);

            if (emailSent)
            {
                _logger.LogInformation("Successfully sent {Type} notification to {Email} for document {DocId}/{Version} at Vietnam time {VietnamTime}",
                    type, user.Email, document.DocumentId, document.Version, vietnamTime);
            }
            else
            {
                _logger.LogError("Failed to send {Type} notification to {Email} for document {DocId}/{Version}",
                    type, user.Email, document.DocumentId, document.Version);
            }

            // Send SignalR notification
            await SendSignalRNotificationAsync(user, type, subject, document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to {Email} for document {DocId}/{Version}",
                user.Email, document.DocumentId, document.Version);
        }
    }

    private async Task TryUpdateDocumentStatusAsync(DocumentExpirationDto document)
    {
        try
        {
            if (string.IsNullOrEmpty(document.DocumentId) || string.IsNullOrEmpty(document.Version))
            {
                _logger.LogWarning("Cannot update document status: DocumentId or Version is null/empty");
                return;
            }

            // ✅ Log timezone information
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime();
            _logger.LogInformation("Updating document {DocId}/{Version} status to Archived at Vietnam time: {VietnamTime}",
                document.DocumentId, document.Version, vietnamTime);

            var updateClient = _serviceProvider.GetService<IRequestClient<UpdateDocumentStatusCommand>>();
            if (updateClient == null)
            {
                _logger.LogError("UpdateDocumentStatusCommand client is not registered");
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var response = await updateClient.GetResponse<UpdateDocumentStatusResponse>(
        new UpdateDocumentStatusCommand
        {
            DocumentId = document.DocumentId,
            Version = document.Version,
            NewStatus = "Archived",
            UpdateKernelMemory = true,    
            VietnamTime = vietnamTime,  
            UpdatedBy = "system_expiration", 
            RequestId = Guid.NewGuid()
        },
        timeout.Token
    );

            if (response?.Message?.Success == true)
            {
                _logger.LogInformation("Successfully updated document {DocId}/{Version} from {OldStatus} to {NewStatus} at Vietnam time {VietnamTime}. Kernel Memory updated: {KMUpdated}",
                    document.DocumentId, document.Version, response.Message.OldStatus, response.Message.NewStatus, vietnamTime, response.Message.KernelMemoryUpdated);

                // ✅ ADD: Lưu log cho việc archive document
                var archiveLog = new NotificationLog
                {
                    DocumentId = document.DocumentId,
                    DocumentVersion = document.Version,
                    NotificationType = NotificationType.General,
                    RecipientType = RecipientType.SystemAlert, // System event
                    RecipientAddress = "system",
                    Subject = $"Document Archived: {document.Title}",
                    Message = $"Document '{document.Title}' (Version: {document.Version}) has been automatically archived due to expiration. Previous status: {response.Message.OldStatus}, New status: {response.Message.NewStatus}. Kernel Memory updated: {response.Message.KernelMemoryUpdated}",
                    IsSent = true,
                    SentAt = vietnamTime, // ✅ Vietnam time
                    ErrorMessage = null
                };

                await _logService.CreateLogAsync(archiveLog);
            }
            else
            {
                _logger.LogError("Failed to update document status for {DocId}/{Version}: {Error}",
                    document.DocumentId, document.Version, response?.Message?.ErrorMessage ?? "Unknown error");

                // ✅ ADD: Lưu log cho việc archive thất bại
                var failureLog = new NotificationLog
                {
                    DocumentId = document.DocumentId,
                    DocumentVersion = document.Version,
                    NotificationType = NotificationType.General,
                    RecipientType = RecipientType.SystemAlert,
                    RecipientAddress = "system",
                    Subject = $"Document Archive Failed: {document.Title}",
                    Message = $"Failed to archive document '{document.Title}' (Version: {document.Version}). Error: {response?.Message?.ErrorMessage ?? "Unknown error"}",
                    IsSent = false,
                    SentAt = vietnamTime,
                    ErrorMessage = response?.Message?.ErrorMessage ?? "Unknown error"
                };

                await _logService.CreateLogAsync(failureLog);
            }
        }
        catch (RequestTimeoutException)
        {
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime();
            _logger.LogError("Timeout updating document status for {DocId}/{Version}",
                document.DocumentId, document.Version);

            // ✅ ADD: Log timeout
            var timeoutLog = new NotificationLog
            {
                DocumentId = document.DocumentId,
                DocumentVersion = document.Version,
                NotificationType = NotificationType.General,
                RecipientType = RecipientType.SystemAlert,
                RecipientAddress = "system",
                Subject = $"Document Archive Timeout: {document.Title}",
                Message = $"Timeout while trying to archive document '{document.Title}' (Version: {document.Version})",
                IsSent = false,
                SentAt = vietnamTime,
                ErrorMessage = "Request timeout"
            };

            await _logService.CreateLogAsync(timeoutLog);
        }
        catch (Exception ex)
        {
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime();
            _logger.LogError(ex, "Error updating document status for {DocId}/{Version}",
                document.DocumentId, document.Version);

            // ✅ ADD: Log exception
            var exceptionLog = new NotificationLog
            {
                DocumentId = document.DocumentId,
                DocumentVersion = document.Version,
                NotificationType = NotificationType.General,
                RecipientType = RecipientType.SystemAlert,
                RecipientAddress = "system",
                Subject = $"Document Archive Error: {document.Title}",
                Message = $"Error while trying to archive document '{document.Title}' (Version: {document.Version}). Exception: {ex.Message}",
                IsSent = false,
                SentAt = vietnamTime,
                ErrorMessage = ex.Message
            };

            await _logService.CreateLogAsync(exceptionLog);
        }
    }
    public async Task ProcessWeeklyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName)
    {
        try
        {
            _logger.LogInformation("Processing weekly grouped notification for {DepartmentName} with {Count} documents",
                departmentName, documents.Count);

            // ✅ Get template (fallback to individual if not found)
            var template = await _emailTemplateService.GetEmailTemplateByNameAsync("WeeklyDocumentExpiration");
            if (template == null)
            {
                _logger.LogWarning("Weekly template not found, using individual notifications for {DepartmentName}", departmentName);
                // Fallback: send individual notifications
                foreach (var doc in documents)
                {
                    await ProcessNearingExpirationNotification(doc);
                }
                return;
            }

            // ✅ Get recipients for the department
            var recipients = await GetDepartmentRecipientsAsync(documents.First().DepartmentId);
            if (!recipients.Any())
            {
                _logger.LogWarning("No recipients found for department {DepartmentName}", departmentName);
                return;
            }

            // ✅ Create grouped content
            var documentsListHtml = CreateDocumentsListHtml(documents);
            var weekRange = GetCurrentWeekRange();
            var subject = $"[{departmentName}] Thông báo tuần: {documents.Count} tài liệu sắp hết hạn";

            // ✅ Send to all department recipients
            var notificationTasks = recipients.Select(async user =>
            {
                try
                {
                    await SendWeeklyGroupedNotificationAsync(
                        user.Email,
                        user.Name,
                        subject,
                        template.TemplateName,
                        departmentName,
                        documents.Count,
                        documentsListHtml,
                        weekRange,
                        documents.First().DepartmentId.ToString(),
                        user.UserId);

                    _logger.LogDebug("Weekly grouped notification sent to {UserEmail}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send weekly grouped notification to {UserEmail}", user.Email);
                }
            });

            await Task.WhenAll(notificationTasks);

            _logger.LogInformation("Weekly grouped notifications sent to {Count} users for {DepartmentName}",
                recipients.Count, departmentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weekly grouped notification for {DepartmentName}", departmentName);
        }
    }
    private async Task SendWeeklyGroupedNotificationAsync(
    string recipientEmail,
    string recipientName,
    string subject,
    string templateName,
    string departmentName,
    int documentCount,
    string documentsListHtml,
    string weekRange,
    string departmentId,
    Guid userId)
    {
        try
        {
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime(); // ✅ ADD: Vietnam time

            // ✅ Check for recent weekly notification to avoid duplicates
            var logRepo = _unitOfWork.GetRepository<NotificationLog>();
            var last7Days = DateTime.UtcNow.AddDays(-7);

            var alreadySent = await logRepo.AnyAsync(l =>
                l.DocumentId == "WEEKLY_GROUP" &&
                l.DocumentVersion == departmentId &&
                l.RecipientAddress == recipientEmail &&
                l.NotificationType == NotificationType.General &&
                l.IsSent == true &&
                l.SentAt >= last7Days);

            if (alreadySent)
            {
                _logger.LogDebug("Weekly notification already sent to {Email} for department {DeptId} in last 7 days",
                    recipientEmail, departmentId);
                return;
            }

            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogError("Template '{TemplateName}' not found", templateName);
                return;
            }

            // ✅ Replace placeholders (similar to existing pattern)
            var emailBody = template.BodyHtml
                .Replace("{{RecipientName}}", SanitizeValue(recipientName))
                .Replace("{{RecipientEmail}}", SanitizeValue(recipientEmail))
                .Replace("{{DepartmentName}}", SanitizeValue(departmentName))
                .Replace("{{DocumentCount}}", documentCount.ToString())
                .Replace("{{DocumentsList}}", documentsListHtml)
                .Replace("{{WeekRange}}", SanitizeValue(weekRange));

            var emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, emailBody);

            // ✅ Log as General notification (reuse existing enum)
            var log = new NotificationLog
            {
                DocumentId = "WEEKLY_GROUP", // Special identifier
                DocumentVersion = departmentId, // Store department ID here
                NotificationType = NotificationType.General, // Reuse existing type
                RecipientType = RecipientType.Email,
                RecipientAddress = recipientEmail,
                Subject = subject,
                Message = emailBody,
                IsSent = emailSent,
                SentAt = emailSent ? vietnamTime : null,
                ErrorMessage = emailSent ? null : "Failed to send weekly grouped notification"
            };

            await _logService.CreateLogAsync(log);

            // ✅ Send SignalR notification (similar to existing pattern)
            if (emailSent)
            {
                await SendSignalRNotificationAsync(
                    userId,
                    subject,
                    $"Weekly document expiration summary for {departmentName}",
                    Guid.NewGuid()); // Use new GUID as placeholder
            }

            _logger.LogInformation("Successfully sent weekly grouped notification to {Email} for {DepartmentName}",
                recipientEmail, departmentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending weekly grouped notification to {Email}", recipientEmail);
        }
    }
   

    // ✅ Helper methods (simple implementations)
    private string CreateDocumentsListHtml(List<DocumentExpirationDto> documents)
    {
        var html = "<ul style='line-height: 1.6;'>";
        var vietnamDate = VietnamTimeHelper.GetVietnamDate(); // ✅ FIX: Dùng Vietnam date

        foreach (var doc in documents.OrderBy(d => d.EffectiveUntil))
        {
            var daysLeft = doc.EffectiveUntil.HasValue
                ? (doc.EffectiveUntil.Value.Date - vietnamDate).Days  // ✅ FIX: Vietnam date
                : 0;

            var statusColor = daysLeft <= 3 ? "color: #d9534f;" : "color: #f0ad4e;";

            var documentLink = !string.IsNullOrEmpty(doc.DocumentLink)
                ? doc.DocumentLink
                : $"https://docai.asia/document/{doc.DocumentId}";

            html += $@"
        <li style='margin-bottom: 15px; padding: 12px; border-left: 3px solid #f0ad4e; background-color: #fefefe; border-radius: 4px;'>
            <div style='margin-bottom: 6px;'>
                <strong style='{statusColor}'>{SanitizeValue(doc.Title)}</strong>
                <a href='{documentLink}' style='margin-left: 10px; color: #007bff; text-decoration: none; font-size: 12px;'>
                    🔗 Xem tài liệu
                </a>
            </div>
            <div style='font-size: 13px; color: #6c757d;'>
                Phiên bản: {SanitizeValue(doc.Version)} | 
                Hết hạn: {doc.EffectiveUntil?.ToString("dd/MM/yyyy")} 
                <span style='{statusColor}; font-weight: bold;'>({daysLeft} ngày nữa)</span>
            </div>
        </li>";
        }
        html += "</ul>";
        return html;
    }

    private string GetCurrentWeekRange()
    {
        var vietnamToday = VietnamTimeHelper.GetVietnamDate(); // ✅ FIX: Dùng Vietnam date
        var startOfWeek = vietnamToday.AddDays(-(int)vietnamToday.DayOfWeek + 1); // Monday
        var endOfWeek = startOfWeek.AddDays(6); // Sunday

        return $"{startOfWeek:dd/MM/yyyy} - {endOfWeek:dd/MM/yyyy}";
    }

    private async Task<List<UserDto>> GetDepartmentRecipientsAsync(Guid departmentId)
    {
        try
        {
            var recipients = new List<UserDto>();

            // ✅ Get department managers, editors, and users (similar to existing pattern)
            var managers = await _userService.GetDepartmentManagersAsync(departmentId);
            var editors = await _userService.GetDepartmentEditorsAsync(departmentId);
            var departmentUsers = await _userService.GetUsersByDepartmentAsync(departmentId);

            recipients.AddRange(managers);
            recipients.AddRange(editors);
            recipients.AddRange(departmentUsers);

            // ✅ Remove duplicates (similar to existing pattern)
            var uniqueRecipients = recipients
                .Where(r => !string.IsNullOrEmpty(r.Email))
                .GroupBy(r => r.Email.ToLower())
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Found {Count} recipients for department {DepartmentId}",
                uniqueRecipients.Count, departmentId);

            return uniqueRecipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department recipients for {DepartmentId}", departmentId);
            return new List<UserDto>();
        }
    }

    // ✅ Reuse existing method pattern
    private static string SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[Không có thông tin]";

        return value.Replace("<", "&lt;").Replace(">", "&gt;").Trim();
    }

    // ✅ Reuse existing SignalR method
    private async Task SendSignalRNotificationAsync(
        Guid userId,
        string subject,
        string message,
        Guid documentId)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
            {
                Type = "WeeklyExpiration",
                Subject = subject,
                Message = message,
                Timestamp = DateTime.UtcNow,
                DocumentId = documentId
            });

            _logger.LogDebug("SignalR weekly notification sent to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR weekly notification to user {UserId}", userId);
        }
    }
}
