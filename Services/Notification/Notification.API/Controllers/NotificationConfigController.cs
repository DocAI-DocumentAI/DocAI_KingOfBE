using Microsoft.AspNetCore.Mvc;
using Notification.API.Payload.Request;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/notification-config")]
    // [Authorize(Roles = "Admin")] // Chỉ Admin mới có quyền quản lý cấu hình
    public class NotificationConfigController : ControllerBase
    {
        private readonly INotificationConfigService _configService;
        private readonly ILogger<NotificationConfigController> _logger;

        public NotificationConfigController(INotificationConfigService configService, ILogger<NotificationConfigController> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(NotificationConfigResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNotificationConfig()
        {
            var config = await _configService.GetNotificationConfigAsync();
            return Ok(config);
        }

        [HttpPut]
        [ProducesResponseType(typeof(NotificationConfigResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateNotificationConfig([FromBody] NotificationConfigRequest request)
        {
            _logger.LogInformation("Attempting to update notification configuration.");
            var response = await _configService.UpdateNotificationConfigAsync(request);
            return Ok(response);
        }
    }
}
