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
                var userSetting = await _userSettingService.GetUserSettingByUserIdAsync(command.UserId);

                var notificationsEnabled = userSetting?.NotificationsEnabled ?? true;

                var preferences = new UserNotificationPreferencesDto
                {
                    UserId = command.UserId,
                    NotificationsEnabled = notificationsEnabled,
                    EmailNotificationsEnabled = notificationsEnabled,
                    SystemNotificationsEnabled = notificationsEnabled,
                    DocumentWorkflowEnabled = notificationsEnabled,
                    DocumentExpirationEnabled = notificationsEnabled,
                    DocumentSubmissionEnabled = notificationsEnabled,
                    DocumentApprovalEnabled = notificationsEnabled,
                    DocumentRejectionEnabled = notificationsEnabled
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
                _logger.LogError(ex, "Error getting user notification preferences for {UserId}", context.Message.UserId);
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