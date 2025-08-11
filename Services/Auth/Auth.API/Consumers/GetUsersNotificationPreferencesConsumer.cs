using Auth.API.Services.Interface;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetUsersNotificationPreferencesConsumer : IConsumer<GetUsersNotificationPreferencesCommand>
    {
        private readonly IUserSettingService _userSettingService;
        private readonly ILogger<GetUsersNotificationPreferencesConsumer> _logger;

        public GetUsersNotificationPreferencesConsumer(
            IUserSettingService userSettingService,
            ILogger<GetUsersNotificationPreferencesConsumer> logger)
        {
            _userSettingService = userSettingService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetUsersNotificationPreferencesCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting notification preferences for {Count} users", command.UserIds.Count);

                var preferences = new List<UserNotificationPreferencesDto>();

                foreach (var userId in command.UserIds)
                {
                    var userSetting = await _userSettingService.GetUserSettingByUserIdAsync(userId);

                    preferences.Add(new UserNotificationPreferencesDto
                    {
                        UserId = userId,
                        NotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                        EmailNotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                        SystemNotificationsEnabled = userSetting?.NotificationsEnabled ?? true,
                        DocumentWorkflowEnabled = userSetting?.NotificationsEnabled ?? true,
                        DocumentExpirationEnabled = userSetting?.NotificationsEnabled ?? true,
                        DocumentSubmissionEnabled = userSetting?.NotificationsEnabled ?? true,
                        DocumentApprovalEnabled = userSetting?.NotificationsEnabled ?? true,
                        DocumentRejectionEnabled = userSetting?.NotificationsEnabled ?? true
                    });
                }

                await context.RespondAsync(new GetUsersNotificationPreferencesResponse
                {
                    Preferences = preferences,
                    Success = true,
                    RequestId = command.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users notification preferences");
                await context.RespondAsync(new GetUsersNotificationPreferencesResponse
                {
                    Preferences = new List<UserNotificationPreferencesDto>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}
