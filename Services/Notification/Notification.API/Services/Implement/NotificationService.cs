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
using Shared.Utils;

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

        // ✅ Use unified TimeZone helper
        var daysFromToday = TimeZoneHelper.DaysFromToday(effectiveUntil.Value);

        if (daysFromToday < 0)
            return $"Đã hết hạn {Math.Abs(daysFromToday)} ngày";
        else if (daysFromToday == 0)
            return "Hết hạn hôm nay";
        else
            return $"Còn {daysFromToday} ngày";
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
                Timestamp = TimeZoneHelper.UtcNow, // ✅ Use unified helper
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
                SentAt = emailSent ? TimeZoneHelper.UtcNow : null, // ✅ Use unified helper
                CreateAt = TimeZoneHelper.UtcNow // ✅ Use unified helper
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

        // Try to update document status to Archived
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
            var processingId = Guid.NewGuid();
            var utcNow = TimeZoneHelper.UtcNow; // ✅ Use unified helper

            _logger.LogInformation("Attempting to send {Type} notification to {Email} for document {DocId}/{Version}",
                type, user.Email, document.DocumentId, document.Version);

            var logRepo = _unitOfWork.GetRepository<NotificationLog>();

            // Check for duplicates
            var checkPeriod = type == NotificationType.Expired
                ? TimeZoneHelper.UtcNow.AddDays(-1)  // ✅ Use unified helper
                : TimeZoneHelper.UtcNow.AddDays(-7); // ✅ Use unified helper

            var existingNotification = await logRepo.AnyAsync(l =>
                l.DocumentId == document.DocumentId &&
                l.DocumentVersion == document.Version &&
                l.NotificationType == type &&
                l.RecipientAddress == user.Email &&
                l.IsSent == true &&
                l.SentAt >= checkPeriod);

            if (existingNotification)
            {
                _logger.LogDebug("Duplicate notification skipped for {Email}", user.Email);
                return;
            }

            // Create processing record with UTC time
            var processingLog = new NotificationLog
            {
                Id = processingId,
                DocumentId = document.DocumentId,
                DocumentVersion = document.Version,
                NotificationType = type,
                RecipientType = RecipientType.Email,
                RecipientAddress = user.Email,
                Subject = "PROCESSING...",
                Message = $"Processing_{utcNow:yyyyMMddHHmmss}",
                IsSent = false,
                SentAt = null,
                CreateAt = utcNow,  // ✅ Always UTC for database
                ErrorMessage = "Processing in progress..."
            };

            try
            {
                await logRepo.InsertAsync(processingLog);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to claim notification processing for {Email}: {Error}",
                    user.Email, ex.Message);
                return;
            }

            // Get template and prepare content
            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogError("Template '{TemplateName}' not found", templateName);
                processingLog.ErrorMessage = $"Template '{templateName}' not found";
                logRepo.UpdateAsync(processingLog);
                await _unitOfWork.CommitAsync();
                return;
            }

            // Prepare email content
            var documentLink = $"https://docai.asia/document/{document.DocumentId}";
            var expirationStatus = type == NotificationType.Expired ? "đã hết hạn" : "sắp hết hạn";

            // ✅ For display purposes, convert UTC to Vietnam time using unified helper
            var vietnamTimeForDisplay = TimeZoneHelper.ConvertUtcToVietnam(utcNow);
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
                .Replace("{{VietnamTime}}", vietnamTimeForDisplay.ToString("dd/MM/yyyy HH:mm")); // ✅ Display only

            var subject = type == NotificationType.Expired
                ? $"[{document.DepartmentName}] Tài liệu '{document.Title}' đã hết hạn"
                : $"[{document.DepartmentName}] Tài liệu '{document.Title}' sắp hết hạn";

            // Send email
            var emailSent = await _emailService.SendEmailAsync(user.Email, subject, emailBody);

            // ✅ Update with UTC time using unified helper
            processingLog.Subject = subject;
            processingLog.Message = emailBody;
            processingLog.IsSent = emailSent;
            processingLog.SentAt = emailSent ? TimeZoneHelper.UtcNow : null; // ✅ Always UTC
            processingLog.ErrorMessage = emailSent ? null : "Failed to send email notification";

            logRepo.UpdateAsync(processingLog);
            await _unitOfWork.CommitAsync();

            if (emailSent)
            {
                _logger.LogInformation("Successfully sent {Type} notification to {Email} for document {DocId}/{Version}",
                    type, user.Email, document.DocumentId, document.Version);

                await SendSignalRNotificationAsync(user, type, subject, document);
            }
            else
            {
                _logger.LogError("Failed to send {Type} notification to {Email} for document {DocId}/{Version}",
                    type, user.Email, document.DocumentId, document.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending notification to {Email}", user.Email);
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

            // ✅ Use unified helper for Vietnam time display
            var vietnamTime = TimeZoneHelper.VietnamNow;
            _logger.LogInformation("Updating document {DocId}/{Version} status to Archived at Vietnam time: {VietnamTime}",
                document.DocumentId, document.Version, vietnamTime.ToString("yyyy-MM-dd HH:mm:ss"));

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
                    VietnamTime = vietnamTime, // ✅ Pass Vietnam time for logging
                    UpdatedBy = "system_expiration",
                    RequestId = Guid.NewGuid()
                },
                timeout.Token);

            if (response?.Message?.Success == true)
            {
                _logger.LogInformation("Successfully updated document {DocId}/{Version} from {OldStatus} to {NewStatus}",
                    document.DocumentId, document.Version, response.Message.OldStatus, response.Message.NewStatus);

                var archiveLog = new NotificationLog
                {
                    DocumentId = document.DocumentId,
                    DocumentVersion = document.Version,
                    NotificationType = NotificationType.General,
                    RecipientType = RecipientType.SystemAlert,
                    RecipientAddress = "system",
                    Subject = $"Document Archived: {document.Title}",
                    Message = $"Document '{document.Title}' has been automatically archived due to expiration.",
                    IsSent = true,
                    SentAt = TimeZoneHelper.UtcNow,     // ✅ Store UTC in database
                    CreateAt = TimeZoneHelper.UtcNow,   // ✅ Store UTC in database
                    ErrorMessage = null
                };

                await _logService.CreateLogAsync(archiveLog);
            }
            else
            {
                _logger.LogError("Failed to update document status for {DocId}/{Version}: {Error}",
                    document.DocumentId, document.Version, response?.Message?.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document status for {DocId}/{Version}",
                document.DocumentId, document.Version);
        }
    }

    public async Task ProcessWeeklyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName)
    {
        await ProcessGroupedNotificationAsync(documents, departmentName, "Weekly", "WeeklyDocumentExpiration", 7);
    }

    // ✅ NEW: Daily grouped notification method
    public async Task ProcessDailyGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName)
    {
        await ProcessGroupedNotificationAsync(documents, departmentName, "Daily", "DailyDocumentExpiration", 1);
    }

    private async Task ProcessGroupedNotificationAsync(List<DocumentExpirationDto> documents, string departmentName,
        string groupType, string templateName, int duplicateCheckDays)
    {
        try
        {
            _logger.LogInformation("Processing {GroupType} grouped notification for {DepartmentName} with {Count} documents",
                groupType, departmentName, documents.Count);

            // Try primary template first, fallback to WeeklyDocumentExpiration
            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null && templateName != "WeeklyDocumentExpiration")
            {
                _logger.LogWarning("Template '{TemplateName}' not found, falling back to 'WeeklyDocumentExpiration'", templateName);
                template = await _emailTemplateService.GetEmailTemplateByNameAsync("WeeklyDocumentExpiration");
            }

            if (template == null)
            {
                _logger.LogWarning("No suitable template found, using individual notifications for {DepartmentName}", departmentName);

                // Fallback: send individual notifications
                foreach (var doc in documents)
                {
                    await ProcessNearingExpirationNotification(doc);
                }
                return;
            }

            // Get recipients for the department
            var recipients = await GetDepartmentRecipientsAsync(documents.First().DepartmentId);
            if (!recipients.Any())
            {
                _logger.LogWarning("No recipients found for department {DepartmentName}", departmentName);
                return;
            }

            // Create grouped content with dynamic text based on groupType
            var documentsListHtml = CreateDocumentsListHtml(documents);
            var timeRange = groupType == "Weekly" ? GetCurrentWeekRange() : TimeZoneHelper.VietnamNow.ToString("dd/MM/yyyy");
            var subject = $"[{departmentName}] Thông báo {groupType.ToLower()}: {documents.Count} tài liệu sắp hết hạn";

            // Send to all department recipients
            var notificationTasks = recipients.Select(async user =>
            {
                try
                {
                    await SendGroupedNotificationAsync(
                        user.Email,
                        user.Name,
                        subject,
                        template.TemplateName,
                        departmentName,
                        documents.Count,
                        documentsListHtml,
                        timeRange,
                        documents.First().DepartmentId.ToString(),
                        user.UserId,
                        groupType,
                        duplicateCheckDays);

                    _logger.LogDebug("{GroupType} grouped notification sent to {UserEmail}", groupType, user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send {GroupType} grouped notification to {UserEmail}", groupType, user.Email);
                }
            });

            await Task.WhenAll(notificationTasks);

            _logger.LogInformation("{GroupType} grouped notifications sent to {Count} users for {DepartmentName}",
                groupType, recipients.Count, departmentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {GroupType} grouped notification for {DepartmentName}", groupType, departmentName);
        }
    }

    private async Task SendGroupedNotificationAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string templateName,
        string departmentName,
        int documentCount,
        string documentsListHtml,
        string timeRange,
        string departmentId,
        Guid userId,
        string groupType,
        int duplicateCheckDays)
    {
        try
        {
            var utcNow = TimeZoneHelper.UtcNow; // ✅ Use unified helper

            // Check for recent grouped notification
            var logRepo = _unitOfWork.GetRepository<NotificationLog>();
            var cutoffTime = TimeZoneHelper.UtcNow.AddDays(-duplicateCheckDays); // ✅ Use unified helper

            var alreadySent = await logRepo.AnyAsync(l =>
                l.DocumentId == $"{groupType.ToUpper()}_GROUP" &&
                l.DocumentVersion == departmentId &&
                l.RecipientAddress == recipientEmail &&
                l.NotificationType == NotificationType.General &&
                l.IsSent == true &&
                l.SentAt >= cutoffTime);

            if (alreadySent)
            {
                _logger.LogDebug("{GroupType} notification already sent to {Email}", groupType, recipientEmail);
                return;
            }

            var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
            if (template == null)
            {
                _logger.LogError("Template '{TemplateName}' not found", templateName);
                return;
            }

            // ✅ Convert to Vietnam time for display using unified helper
            var vietnamTimeForDisplay = TimeZoneHelper.ConvertUtcToVietnam(utcNow);

            var emailBody = template.BodyHtml
                .Replace("{{RecipientName}}", SanitizeValue(recipientName))
                .Replace("{{RecipientEmail}}", SanitizeValue(recipientEmail))
                .Replace("{{UserName}}", SanitizeValue(recipientName))
                .Replace("{{UserEmail}}", SanitizeValue(recipientEmail))
                .Replace("{{DepartmentName}}", SanitizeValue(departmentName))
                .Replace("{{DocumentCount}}", documentCount.ToString())
                .Replace("{{DocumentsList}}", documentsListHtml)
                .Replace("{{WeekRange}}", SanitizeValue(timeRange))
                .Replace("{{TimeRange}}", SanitizeValue(timeRange))
                .Replace("{{GroupType}}", groupType)
                .Replace("{{NotificationType}}", groupType.ToLower())
                .Replace("{{VietnamTime}}", vietnamTimeForDisplay.ToString("dd/MM/yyyy HH:mm")); // ✅ Display only

            var emailSent = await _emailService.SendEmailAsync(recipientEmail, subject, emailBody);

            // ✅ Create log with UTC time using unified helper
            var log = new NotificationLog
            {
                DocumentId = $"{groupType.ToUpper()}_GROUP",
                DocumentVersion = departmentId,
                NotificationType = NotificationType.General,
                RecipientType = RecipientType.Email,
                RecipientAddress = recipientEmail,
                Subject = subject,
                Message = emailBody,
                IsSent = emailSent,
                SentAt = emailSent ? TimeZoneHelper.UtcNow : null, // ✅ UTC
                CreateAt = TimeZoneHelper.UtcNow,                  // ✅ UTC
                ErrorMessage = emailSent ? null : $"Failed to send {groupType.ToLower()} grouped notification"
            };

            await _logService.CreateLogAsync(log);

            _logger.LogInformation("Successfully sent {GroupType} grouped notification to {Email}",
                groupType, recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {GroupType} grouped notification to {Email}", groupType, recipientEmail);
        }
    }


    // Helper methods
    private string CreateDocumentsListHtml(List<DocumentExpirationDto> documents)
    {
        var html = "<ul style='line-height: 1.6;'>";
        var vietnamToday = TimeZoneHelper.VietnamToday; // ✅ Use unified helper

        foreach (var doc in documents.OrderBy(d => d.EffectiveUntil))
        {
            var daysLeft = doc.EffectiveUntil.HasValue
                ? TimeZoneHelper.DaysFromToday(doc.EffectiveUntil.Value) // ✅ Use unified helper
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
        var vietnamToday = TimeZoneHelper.VietnamToday; // ✅ Use unified helper
        var startOfWeek = vietnamToday.AddDays(-(int)vietnamToday.DayOfWeek + 1); // Monday
        var endOfWeek = startOfWeek.AddDays(6); // Sunday

        return $"{startOfWeek:dd/MM/yyyy} - {endOfWeek:dd/MM/yyyy}";
    }

    private async Task<List<UserDto>> GetDepartmentRecipientsAsync(Guid departmentId)
    {
        try
        {
            var allRecipients = new List<UserDto>();

            var managers = await _userService.GetDepartmentManagersAsync(departmentId);
            var editors = await _userService.GetDepartmentEditorsAsync(departmentId);
            var departmentUsers = await _userService.GetUsersByDepartmentAsync(departmentId);

            allRecipients.AddRange(managers);
            allRecipients.AddRange(editors);
            allRecipients.AddRange(departmentUsers);

            // Improved deduplication
            var uniqueRecipients = allRecipients
                .Where(r => !string.IsNullOrEmpty(r.Email))
                .GroupBy(r => new { Email = r.Email.ToLower(), UserId = r.UserId })
                .Select(g => g.First())
                .ToList();
            return uniqueRecipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department recipients for {DepartmentId}", departmentId);
            return new List<UserDto>();
        }
    }

    private static string SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[Không có thông tin]";

        return value.Replace("<", "&lt;").Replace(">", "&gt;").Trim();
    }
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
                Type = "GroupedExpiration",
                Subject = subject,
                Message = message,
                Timestamp = TimeZoneHelper.UtcNow, // ✅ Use unified helper
                DocumentId = documentId
            });

            _logger.LogDebug("SignalR grouped notification sent to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR grouped notification to user {UserId}", userId);
        }
    }
}
