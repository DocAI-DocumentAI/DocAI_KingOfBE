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

namespace Notification.API.Services.Implement;

public class NotificationService : INotificationService
{
    private const string ROLE_CACHE_KEY = "AllRolesCache";
    private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
    private readonly ILogger<NotificationService> _logger;
    private readonly IMapper _mapper;
    private readonly IAuthClient _authClient;
    private readonly IEmailService _emailService;
    private readonly IDocumentClient _documentClient;
    private readonly IMemoryCache _cache;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly INotificationLogService _logService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly TemplateRendererUtil _templateRenderer;
    private readonly IConfiguration _configuration;
    public NotificationService(
             IUnitOfWork<NotificationDbContext> unitOfWork,
             ILogger<NotificationService> logger,
             IMapper mapper,
             IAuthClient authClient,
             IEmailService emailService,
             IDocumentClient documentClient,
             IMemoryCache cache,
             IEmailTemplateService emailTemplateService,
             INotificationLogService logService,
             IHubContext<NotificationHub> hubContext,
             TemplateRendererUtil templateRenderer,
             IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _authClient = authClient;
        _emailService = emailService;
        _documentClient = documentClient;
        _cache = cache;
        _emailTemplateService = emailTemplateService;
        _logService = logService;
        _hubContext = hubContext;
        _templateRenderer = templateRenderer;
        _configuration = configuration;
    }
    public async Task ProcessNearingExpirationNotification(DocumentDetailResponseExternal document)
    {
        await ProcessDocumentNotification(document, NotificationType.NearingExpiration, "DocumentNearingExpiration");
    }
    public async Task ProcessExpiredDocumentNotification(DocumentDetailResponseExternal document)
    {
        await ProcessDocumentNotification(document, NotificationType.Expired, "DocumentExpired");

        // BR-038: Automatically update status to 'Expired' after sending notification
        bool updated = await _documentClient.UpdateDocumentStatusAsync(document.DocumentId, document.Version, "Expired");
        if (!updated)
        {
            await SendAdminEscalationNotification(document.DocumentId, document.Version,
                "Failed to Update Document Status",
                $"System failed to automatically update status to 'Expired' for document {document.DocumentId} (Version: {document.Version}).");

        }
    }
    private async Task ProcessDocumentNotification(DocumentDetailResponseExternal document, NotificationType notificationType, string templateName)
    {

        var template = await _emailTemplateService.GetEmailTemplateByNameAsync(templateName);
        if (template == null)
        {
            await SendAdminEscalationNotification(document.DocumentId, document.Version, "Email Template Missing", $"Template '{templateName}' not found.");
            return;
        }

        var recipients = await GetRecipientsForDocument(document);
        if (!recipients.Any())
        {
            await SendAdminEscalationNotification(document.DocumentId, document.Version, "No Recipients Found", $"No recipients found for document {document.DocumentId}.");
            return;
        }

        // Gửi thông báo đến từng người nhận
        foreach (var user in recipients)
        {
            // Tạo một token duy nhất cho email này
            var dismissToken = Guid.NewGuid();
            var dismissLink = $"{_configuration["ApiBaseUrl"]}/api/notifications/dismiss-by-token?token={dismissToken}";

            var templateData = new Dictionary<string, string>
                {
                    { "DocumentTitle", document.Title },
                    { "DocumentVersion", document.Version },
                    { "EffectiveUntil", document.EffectiveUntil?.ToString("dd/MM/yyyy") ?? "N/A" },
                    { "DocumentLink", document.DocumentLink ?? "#" },
                    { "DismissLink", dismissLink }
                };

            var subject = _templateRenderer.Render(template.Subject, templateData);
            var body = _templateRenderer.Render(template.BodyHtml, templateData);

            // Quy tắc mới: Gửi cả Email và Thông báo hệ thống
            // 1. Gửi Email
            bool emailSent = await _emailService.SendEmailAsync(user.Email, subject, body);
            var emailLog = new NotificationLog
            {
                DocumentId = document.DocumentId,
                DocumentVersion = document.Version,
                NotificationType = notificationType,
                RecipientType = RecipientType.Email,
                RecipientAddress = user.Email,
                Subject = subject,
                Message = body,
                IsSent = emailSent,
                SentAt = emailSent ? DateTime.UtcNow : null,
                DismissToken = dismissToken // Lưu token vào log
            };
            await _logService.CreateLogAsync(emailLog);

            // 2. Gửi thông báo hệ thống (SignalR)
            await _hubContext.Clients.User(user.UserId.ToString()).SendAsync("ReceiveNotification", new
            {
                LogId = emailLog.Id, // Gửi kèm LogId để UI có thể thực hiện hành động "Bỏ qua"
                Type = notificationType.ToString(),
                Subject = subject,
                Message = $"Tài liệu '{document.Title}' (phiên bản {document.Version}) {(notificationType == NotificationType.Expired ? "đã hết hạn" : "sắp hết hạn")}.",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task<List<UserDetailResponseExternal>> GetRecipientsForDocument(DocumentDetailResponseExternal document)
    {
        var roles = await GetRolesFromCacheAsync();
        var editorRole = roles.FirstOrDefault(r => r.RoleName.Equals("Editor", StringComparison.OrdinalIgnoreCase));
        var managerRole = roles.FirstOrDefault(r => r.RoleName.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        var recipientList = new List<UserDetailResponseExternal>();
        var tasks = new List<Task<List<UserDetailResponseExternal>>>();

        if (editorRole != null)
            tasks.Add(_authClient.GetUsersByDepartmentAndRoleAsync(document.DepartmentId, editorRole.Id));
        if (managerRole != null)
            tasks.Add(_authClient.GetUsersByDepartmentAndRoleAsync(document.DepartmentId, managerRole.Id));
        if (adminRole != null)
            tasks.Add(_authClient.GetAdminUsersAsync(adminRole.Id));

        var results = await Task.WhenAll(tasks);
        foreach (var userList in results)
        {
            recipientList.AddRange(userList);
        }

        return recipientList.DistinctBy(u => u.UserId).ToList();
    }
    public async Task SendAdminEscalationNotification(Guid? documentId, string? documentVersion, string subject, string errorMessage)
    {
        _logger.LogError("ADMIN_ESCALATION: DocId: {DocId}, Version: {Version}, Error: {Error}", documentId, documentVersion, errorMessage);

        var roles = await GetRolesFromCacheAsync();
        var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        if (adminRole == null)
        {
            _logger.LogCritical("CRITICAL: 'Admin' role not found in the system. Cannot send escalation email.");
            return;
        }

        var adminUsers = await _authClient.GetAdminUsersAsync(adminRole.Id);
        if (!adminUsers.Any())
        {
            _logger.LogCritical("CRITICAL: No admin users found to send escalation email.");
            return;
        }

        var fullSubject = $"[DocAI System Alert] {subject}";
        var body = $"<p>An important system event requires your attention:</p><ul><li><b>Document ID:</b> {documentId?.ToString() ?? "N/A"}</li><li><b>Document Version:</b> {documentVersion ?? "N/A"}</li><li><b>Error Details:</b> {errorMessage}</li></ul>";

        foreach (var admin in adminUsers)
        {
            await _emailService.SendEmailAsync(admin.Email, fullSubject, body);
        }
    }

    private async Task<List<RoleResponse>> GetRolesFromCacheAsync()
    {
        if (!_cache.TryGetValue(ROLE_CACHE_KEY, out List<RoleResponse> roles))
        {
            _logger.LogInformation("Role cache miss. Fetching roles from Auth Service.");
            roles = await _authClient.GetAllRolesAsync();
            if (roles.Any())
            {
                _cache.Set(ROLE_CACHE_KEY, roles, TimeSpan.FromMinutes(15));
            }
        }
        return roles ?? new List<RoleResponse>();
    }
    public async Task<bool> DismissNotificationByUserAsync(Guid logId, Guid userId)
    {
        var logRepo = _unitOfWork.GetRepository<NotificationLog>();
        var log = await logRepo.SingleOrDefaultAsync(predicate: l => l.Id == logId);
        return await ProcessDismissalAsync(log, userId);
    }


    public async Task<string> DismissNotificationByTokenAsync(Guid token)
    {
        var logRepo = _unitOfWork.GetRepository<NotificationLog>();
        var log = await logRepo.SingleOrDefaultAsync(predicate: l => l.DismissToken == token);

        if (log != null && log.IsDismissed)
        {
            return $"This notification was already dismissed on {log.DismissedAt:g}.";
        }

        bool success = await ProcessDismissalAsync(log, null);
        if (!success) return "This dismissal link is invalid or has already been used.";

        return "Notifications for this document version have been successfully dismissed.";
    }

    private async Task<bool> ProcessDismissalAsync(NotificationLog log, Guid? userId)
    {
        if (log == null || log.IsDismissed) return false;

        var logRepo = _unitOfWork.GetRepository<NotificationLog>();

        var relatedLogs = await logRepo.GetListAsync(predicate: l =>
            l.DocumentId == log.DocumentId &&
            l.DocumentVersion == log.DocumentVersion);

        log.IsDismissed = true;
        log.DismissedAt = DateTime.UtcNow;
        log.DismissedByUserId = userId;
        log.DismissToken = null; // Vô hiệu hóa token sau khi sử dụng
        log.UpdateAt = DateTime.UtcNow;

        _unitOfWork.GetRepository<NotificationLog>().UpdateAsync(log);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Notification for Document {DocId}/{Version} dismissed by User {UserId}", log.DocumentId, log.DocumentVersion, userId?.ToString() ?? "via Token");

        if (log.DocumentVersion != null)
        {
            await _documentClient.DeactivateDocumentWarningsAsync(log.DocumentId, log.DocumentVersion);
        }

        return true;
    }
    public async Task SendGeneralNotificationAsync(SendGeneralNotificationCommand command)
    {
        _logger.LogInformation("[START] Processing SendGeneralNotificationCommand for template: {TemplateName}", command.TemplateName);
        var template = await _emailTemplateService.GetEmailTemplateByNameAsync(command.TemplateName);
        if (template == null)
        {
            _logger.LogWarning("[EXIT] Template not found: {TemplateName}. Aborting.", command.TemplateName);
            return;
        }
        _logger.LogInformation("Step 1/4: Template '{TemplateName}' found successfully.", command.TemplateName);

        var (uniqueUsers, directEmails) = await ResolveRecipientsAsync(command.Recipients);
        if (!uniqueUsers.Any() && !directEmails.Any())
        {
            _logger.LogWarning("No recipients found for notification with template: {TemplateName}", command.TemplateName);
            return;
        }
        _logger.LogInformation("Step 2/4: Resolved {UserCount} unique users and {EmailCount} direct emails.", uniqueUsers.Count, directEmails.Count);

        // 3. Render nội dung
        var subject = _templateRenderer.Render(template.Subject, command.TemplateData);
        var body = _templateRenderer.Render(template.BodyHtml, command.TemplateData);
        _logger.LogInformation("Step 3/4: Content rendered successfully. Subject: '{Subject}'", subject);

        // 4. Gửi thông báo theo các kênh được chọn
        if (command.Channels.HasFlag(DeliveryChannel.Email))
        {
            var emailRecipients = uniqueUsers.Select(u => u.Email).Concat(directEmails).Distinct();
            await SendEmailsToRecipientsAsync(emailRecipients, subject, body);
        }

        if (command.Channels.HasFlag(DeliveryChannel.SystemAlert) && uniqueUsers.Any())
        {
            await SendSystemAlertsToRecipientsAsync(uniqueUsers, subject, body);
        }

        _logger.LogInformation("Successfully processed notification command for template {TemplateName}", command.TemplateName);
    }
    private async Task<(List<UserDetailResponseExternal> UniqueUsers, List<string> DirectEmails)> ResolveRecipientsAsync(NotificationRecipients recipients)
    {
        // Sử dụng Dictionary để đảm bảo danh sách người dùng là duy nhất (không có user bị trùng lặp)
        var uniqueUserSet = new Dictionary<Guid, UserDetailResponseExternal>();

        // Tạo một danh sách các tác vụ (Task) để gọi API song song
        var tasks = new List<Task<List<UserDetailResponseExternal>>>();

        // 1. Lấy người dùng theo Role và Department
        if (recipients.RoleId.HasValue)
        {
            if (recipients.DepartmentId.HasValue)
            {
                _logger.LogInformation("Resolving recipients by RoleId {RoleId} and DepartmentId {DepartmentId}", recipients.RoleId.Value, recipients.DepartmentId.Value);
                tasks.Add(_authClient.GetUsersByDepartmentAndRoleAsync(recipients.DepartmentId.Value, recipients.RoleId.Value));
            }
            else
            {
                _logger.LogInformation("Resolving recipients by RoleId {RoleId}", recipients.RoleId.Value);
                tasks.Add(_authClient.GetAdminUsersAsync(recipients.RoleId.Value));
            }
        }

        // 2. Lấy người dùng theo danh sách UserID cụ thể
        if (recipients.UserIds?.Any() ?? false)
        {
            _logger.LogInformation("Resolving {Count} specific recipients by UserIds", recipients.UserIds.Count);
            // Giả định AuthClient có một phương thức GetUsersByIdsAsync để tối ưu.
            // Nếu không, bạn sẽ cần lặp qua từng ID và gọi API riêng lẻ.
            // tasks.Add(_authClient.GetUsersByIdsAsync(recipients.UserIds));
        }

        var results = await Task.WhenAll(tasks);

        foreach (var userList in results)
        {
            foreach (var user in userList)
            {
                uniqueUserSet.TryAdd(user.UserId, user);
            }
        }

        // 3. Trả về kết quả
        // - Danh sách người dùng duy nhất đã được phân giải.
        // - Danh sách email trực tiếp được gửi kèm (nếu có).
        return (uniqueUserSet.Values.ToList(), recipients.EmailAddresses ?? new List<string>());
    }
    private async Task SendEmailsToRecipientsAsync(IEnumerable<string> emailAddresses, string subject, string body)
    {
        if (!emailAddresses.Any())
        {
            _logger.LogInformation("Step 4a/4: No email recipients to send to.");
            return;
        }

        _logger.LogInformation("Step 4a/4: Preparing to send emails to {Count} recipients.", emailAddresses.Count());

        foreach (var email in emailAddresses)
        {
            _logger.LogInformation("Attempting to send email to: {Email}", email);

            bool success = await _emailService.SendEmailAsync(email, subject, body);
            var emailLog = new NotificationLog
            {
                // Vì đây là thông báo chung, không gắn với tài liệu nào cụ thể.
                DocumentId = Guid.Empty,
                DocumentVersion = null,
                NotificationType = NotificationType.General,
                RecipientType = RecipientType.Email,
                RecipientAddress = email,
                Subject = subject,
                Message = body,
                IsSent = success,
                SentAt = success ? DateTime.UtcNow : null,
                ErrorMessage = success ? null : "Failed to send via the configured email provider.",
                IsDismissed = false,
                DismissedAt = null,
                DismissedByUserId = null,
                DismissToken = null
            };

            // Gọi đến service log để lưu
            await _logService.CreateLogAsync(emailLog);
        }
    }
    private async Task SendSystemAlertsToRecipientsAsync(IEnumerable<UserDetailResponseExternal> users, string subject, string message)
    {
        if (!users.Any())
        {
            return;
        }

        var userIds = users.Select(u => u.UserId.ToString()).ToList();
        _logger.LogInformation("Step 4b/4: Preparing to send system alerts to {Count} users.", userIds.Count);

        // 1. Gửi thông báo qua SignalR Hub
        // .Clients.Users(userIds) sẽ chỉ gửi đến các kết nối của những user ID này.
        // "ReceiveNotification" là tên sự kiện mà client (frontend) sẽ lắng nghe.
        await _hubContext.Clients.Users(userIds).SendAsync("ReceiveNotification", new
        {
            Type = NotificationType.General.ToString(),
            Subject = subject,
            Message = message, // Có thể là một phiên bản rút gọn của nội dung email
            Timestamp = DateTime.UtcNow
        });

        // 2. Tạo một bản ghi log cho hành động này
        var alertLog = new NotificationLog
        {
            DocumentId = Guid.Empty, // Không gắn với tài liệu cụ thể
            NotificationType = NotificationType.General,
            RecipientType = RecipientType.SystemAlert, // Đánh dấu kênh gửi là SystemAlert
            RecipientAddress = string.Join(",", userIds), // Lưu danh sách user ID đã nhận
            Subject = subject,
            Message = message,
            IsSent = true, // SendAsync của SignalR là "fire-and-forget", ta mặc định là đã gửi thành công
            SentAt = DateTime.UtcNow,
            ErrorMessage = null,
            IsDismissed = false
        };

        // 3. Lưu bản ghi log vào database
        await _logService.CreateLogAsync(alertLog);
    }
}

