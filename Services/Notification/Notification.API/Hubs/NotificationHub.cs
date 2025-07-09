using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Notification.API.Services.Interfaces;
using StackExchange.Redis;

namespace Notification.API.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        public NotificationHub(ILogger<NotificationHub> logger)

        {
            _logger = logger;

        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "User disconnected from NotificationHub with error: {UserId}", Context.UserIdentifier);
            }
            else
            {
                _logger.LogInformation("User disconnected from NotificationHub: {UserId}", Context.UserIdentifier);
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
}
