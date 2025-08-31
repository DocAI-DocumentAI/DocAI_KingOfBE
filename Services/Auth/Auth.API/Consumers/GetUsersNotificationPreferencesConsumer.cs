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

                    // SIMPLIFIED: Dựa trên single NotificationsEnabled flag
                    var notificationsEnabled = userSetting?.NotificationsEnabled ?? true; // Default true nếu không có setting

                    preferences.Add(new UserNotificationPreferencesDto
                    {
                        UserId = userId,

                        // Main flag từ database
                        NotificationsEnabled = notificationsEnabled,

                        // SIMPLIFIED: Tất cả sub-types đều follow NotificationsEnabled flag
                        EmailNotificationsEnabled = notificationsEnabled,
                        SystemNotificationsEnabled = notificationsEnabled,
                        DocumentWorkflowEnabled = notificationsEnabled,
                        DocumentExpirationEnabled = notificationsEnabled,
                        DocumentSubmissionEnabled = notificationsEnabled,
                        DocumentApprovalEnabled = notificationsEnabled,
                        DocumentRejectionEnabled = notificationsEnabled
                    });
                }

                await context.RespondAsync(new GetUsersNotificationPreferencesResponse
                {
                    Preferences = preferences,
                    Success = true,
                    RequestId = command.RequestId
                });

                _logger.LogInformation("Successfully returned notification preferences for {Count} users", preferences.Count);
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
