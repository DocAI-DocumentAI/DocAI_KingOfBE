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

                // SIMPLIFIED: Default preferences based on single NotificationsEnabled flag
                var defaultPreferences = new UserNotificationPreferencesDto
                {
                    UserId = userId,
                    NotificationsEnabled = true, // Default to enabled if not found
                    EmailNotificationsEnabled = true,
                    SystemNotificationsEnabled = true,
                    // SIMPLIFIED: All notification types follow the main NotificationsEnabled flag
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

                    // SIMPLIFIED: Only check NotificationsEnabled (single flag)
                    var notificationsEnabled = preferences?.NotificationsEnabled ?? true; // Default to true

                    if (notificationsEnabled)
                    {
                        filteredUsers.Add(user);
                    }
                    else
                    {
                        _logger.LogDebug("User {UserId} has notifications disabled", user.UserId);
                    }
                }

                _logger.LogInformation("Filtered recipients from {Total} to {Filtered} based on NotificationsEnabled preference",
                    users.Count, filteredUsers.Count);

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

            // SIMPLIFIED: Only check NotificationsEnabled flag
            return preferences.NotificationsEnabled;
        }

        public async Task<bool> ShouldSendSystemNotificationAsync(Guid userId, string notificationType)
        {
            var preferences = await GetUserPreferencesAsync(userId);
            if (preferences == null) return true; // Default to true

            return preferences.NotificationsEnabled;
        }
    }
}
