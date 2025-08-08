using MassTransit;
using Microsoft.Extensions.Logging;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Shared.Command;
using Shared.Models;

namespace Notification.API.Consumers
{
    public class GeneralNotificationConsumer : IConsumer<SendGeneralNotificationCommand>
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;
        private readonly ILogger<GeneralNotificationConsumer> _logger;

        public GeneralNotificationConsumer(
            INotificationService notificationService,
            IUserService userService,
            ILogger<GeneralNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _userService = userService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendGeneralNotificationCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Processing general notification for template: {TemplateName}", command.TemplateName);

                var recipients = await ResolveRecipientsAsync(command.Recipients);
                if (!recipients.Any())
                {
                    _logger.LogWarning("No recipients found for general notification template: {TemplateName}", command.TemplateName);
                    return;
                }

                foreach (var recipient in recipients)
                {
                    try
                    {
                        await _notificationService.SendGeneralNotificationAsync(command.TemplateName, recipient.Email, recipient.Name);
                        _logger.LogDebug("Sent general notification to {Email}", recipient.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send general notification to {Email}", recipient.Email);
                    }
                }

                _logger.LogInformation("Successfully processed general notification for {Count} recipients", recipients.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing general notification for template: {TemplateName}",
                    context.Message.TemplateName);
            }
        }

        private async Task<List<UserInfo>> ResolveRecipientsAsync(NotificationRecipients recipients)
        {
            var resolvedUsers = new List<UserInfo>();

            try
            {
                // Method 1: Specific User IDs
                if (recipients.UserIds?.Any() == true)
                {
                    foreach (var userId in recipients.UserIds)
                    {
                        var user = await _userService.GetUserByIdAsync(userId);
                        if (user != null)
                        {
                            resolvedUsers.Add(user);
                        }
                    }
                }

                // Method 2: Role-based recipients
                if (!string.IsNullOrEmpty(recipients.RoleName))
                {
                    var roleUsers = await _userService.GetUsersByRoleAsync(recipients.RoleName);
                    resolvedUsers.AddRange(roleUsers);
                }

                // Method 3: Department-based recipients
                if (recipients.DepartmentId.HasValue)
                {
                    var deptUsers = await _userService.GetUsersByDepartmentAsync(recipients.DepartmentId.Value);
                    resolvedUsers.AddRange(deptUsers);
                }

                // Method 4: Direct email addresses with user lookup
                if (recipients.EmailAddresses?.Any() == true)
                {
                    foreach (var email in recipients.EmailAddresses)
                    {
                        // Try to find existing user by email, otherwise create anonymous user
                        var existingUser = resolvedUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                        if (existingUser == null)
                        {
                            resolvedUsers.Add(new UserInfo
                            {
                                UserId = Guid.NewGuid(),
                                Email = email,
                                Name = await GetDisplayNameForEmailAsync(email),
                                Department = "External"
                            });
                        }
                    }
                }

                // Remove duplicates by email
                var uniqueUsers = resolvedUsers
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .GroupBy(u => u.Email.ToLower())
                    .Select(g => g.First())
                    .ToList();

                _logger.LogInformation("Resolved {Count} unique recipients from notification command", uniqueUsers.Count);
                return uniqueUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving recipients for general notification");
                return new List<UserInfo>();
            }
        }

        private async Task<string> GetDisplayNameForEmailAsync(string email)
        {
            try
            {
                // Extract name from email if no user found
                var namePart = email.Split('@')[0];
                var words = namePart.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length >= 2)
                {
                    return string.Join(" ", words.Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
                }

                return char.ToUpper(namePart[0]) + namePart.Substring(1).ToLower();
            }
            catch
            {
                return "User";
            }
        }
    }
}
