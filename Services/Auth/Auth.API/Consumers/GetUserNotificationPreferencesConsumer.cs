using Auth.API.Services.Interface;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetUserNotificationPreferencesConsumer : IConsumer<GetUserNotificationPreferencesCommand>
    {
        private readonly IUserSettingService _userSettingService;
        private readonly ILogger<GetUserNotificationPreferencesConsumer> _logger;

        public GetUserNotificationPreferencesConsumer(
            IUserSettingService userSettingService,
            ILogger<GetUserNotificationPreferencesConsumer> logger)
        {
            _userSettingService = userSettingService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetUserNotificationPreferencesCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting notification preferences for user {UserId}", command.UserId);

                var userSetting = await _userSettingService.GetUserSettingByUserIdAsync(command.UserId);

                var preferences = new UserNotificationPreferencesDto
                {
                    UserId = command.UserId,
                    NotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                    EmailNotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                    SystemNotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                    DocumentWorkflowEnabled = userSetting?.NotificationsEnabled ?? true,
                    DocumentExpirationEnabled = userSetting?.NotificationsEnabled ?? true,
                    DocumentSubmissionEnabled = userSetting?.NotificationsEnabled ?? true,
                    DocumentApprovalEnabled = userSetting?.NotificationsEnabled ?? true,
                    DocumentRejectionEnabled = userSetting?.NotificationsEnabled ?? true
                };

                await context.RespondAsync(new GetUserNotificationPreferencesResponse
                {
                    Preferences = preferences,
                    Success = true,
                    RequestId = command.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user notification preferences");
                await context.RespondAsync(new GetUserNotificationPreferencesResponse
                {
                    Preferences = null,
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}