using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Notification.API.Services.Interfaces;
using Shared.Command;
using Shared.DTOs;

namespace Notification.API.Services.Implement
{
    public class UserPreferencesService : IUserPreferencesService
    {
        private readonly IRequestClient<GetUserNotificationPreferencesCommand> _preferencesClient;
        private readonly IRequestClient<GetUsersNotificationPreferencesCommand> _bulkPreferencesClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserPreferencesService> _logger;

        public UserPreferencesService(
            IRequestClient<GetUserNotificationPreferencesCommand> preferencesClient,
            IRequestClient<GetUsersNotificationPreferencesCommand> bulkPreferencesClient,
            IMemoryCache cache,
            ILogger<UserPreferencesService> logger)
        {
            _preferencesClient = preferencesClient;
            _bulkPreferencesClient = bulkPreferencesClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<UserNotificationPreferencesDto?> GetUserPreferencesAsync(Guid userId)
        {
            var cacheKey = $"user_preferences_{userId}";
            if (_cache.TryGetValue(cacheKey, out UserNotificationPreferencesDto? cached) && cached != null)
                return cached;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _preferencesClient.GetResponse<GetUserNotificationPreferencesResponse>(
                    new GetUserNotificationPreferencesCommand { UserId = userId },
                    timeout.Token
                );

                if (response.Message.Success && response.Message.Preferences != null)
                {
                    _cache.Set(cacheKey, response.Message.Preferences, TimeSpan.FromMinutes(15));
                    return response.Message.Preferences;
                }

                // Default preferences if not found
                var defaultPreferences = new UserNotificationPreferencesDto
                {
                    UserId = userId,
                    NotificationsEnabled = true,
                    EmailNotificationsEnabled = true,
                    SystemNotificationsEnabled = true,
                    DocumentWorkflowEnabled = true,
                    DocumentExpirationEnabled = true,
                    DocumentSubmissionEnabled = true,
                    DocumentApprovalEnabled = true,
                    DocumentRejectionEnabled = true
                };

                _cache.Set(cacheKey, defaultPreferences, TimeSpan.FromMinutes(5));
                return defaultPreferences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user preferences for {UserId}", userId);
                return null;
            }
        }

        public async Task<List<UserDto>> FilterUsersByPreferencesAsync(List<UserDto> users, string notificationType)
        {
            if (!users.Any()) return users;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                var response = await _bulkPreferencesClient.GetResponse<GetUsersNotificationPreferencesResponse>(
                    new GetUsersNotificationPreferencesCommand
                    {
                        UserIds = users.Select(u => u.UserId).ToList()
                    },
                    timeout.Token
                );

                if (!response.Message.Success)
                {
                    _logger.LogWarning("Failed to get bulk user preferences, allowing all users");
                    return users;
                }

                var filteredUsers = new List<UserDto>();

                foreach (var user in users)
                {
                    var preferences = response.Message.Preferences.FirstOrDefault(p => p.UserId == user.UserId);

                    if (preferences == null)
                    {
                        // If no preferences found, allow by default
                        filteredUsers.Add(user);
                        continue;
                    }

                    // Check if user has notifications enabled
                    if (!preferences.NotificationsEnabled)
                    {
                        _logger.LogDebug("User {UserId} has notifications disabled", user.UserId);
                        continue;
                    }

                    // Check specific notification type preferences
                    var shouldInclude = notificationType.ToLower() switch
                    {
                        "documentexpiration" => preferences.DocumentExpirationEnabled,
                        "documentsubmission" => preferences.DocumentSubmissionEnabled,
                        "documentapproval" => preferences.DocumentApprovalEnabled,
                        "documentrejection" => preferences.DocumentRejectionEnabled,
                        "documentworkflow" => preferences.DocumentWorkflowEnabled,
                        _ => true // Default to true for unknown types
                    };

                    if (shouldInclude)
                    {
                        filteredUsers.Add(user);
                    }
                    else
                    {
                        _logger.LogDebug("User {UserId} has {NotificationType} notifications disabled",
                            user.UserId, notificationType);
                    }
                }

                _logger.LogInformation("Filtered {Original} users to {Filtered} based on preferences for {Type}",
                    users.Count, filteredUsers.Count, notificationType);

                return filteredUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering users by preferences, allowing all users");
                return users; // Fallback to all users if preferences check fails
            }
        }

        public async Task<bool> ShouldSendEmailNotificationAsync(Guid userId, string notificationType)
        {
            var preferences = await GetUserPreferencesAsync(userId);
            if (preferences == null) return true; // Default to true

            if (!preferences.NotificationsEnabled || !preferences.EmailNotificationsEnabled)
                return false;

            return notificationType.ToLower() switch
            {
                "documentexpiration" => preferences.DocumentExpirationEnabled,
                "documentsubmission" => preferences.DocumentSubmissionEnabled,
                "documentapproval" => preferences.DocumentApprovalEnabled,
                "documentrejection" => preferences.DocumentRejectionEnabled,
                "documentworkflow" => preferences.DocumentWorkflowEnabled,
                _ => true
            };
        }

        public async Task<bool> ShouldSendSystemNotificationAsync(Guid userId, string notificationType)
        {
            var preferences = await GetUserPreferencesAsync(userId);
            if (preferences == null) return true; // Default to true

            if (!preferences.NotificationsEnabled || !preferences.SystemNotificationsEnabled)
                return false;

            return notificationType.ToLower() switch
            {
                "documentexpiration" => preferences.DocumentExpirationEnabled,
                "documentsubmission" => preferences.DocumentSubmissionEnabled,
                "documentapproval" => preferences.DocumentApprovalEnabled,
                "documentrejection" => preferences.DocumentRejectionEnabled,
                "documentworkflow" => preferences.DocumentWorkflowEnabled,
                _ => true
            };
        }
    }
}
