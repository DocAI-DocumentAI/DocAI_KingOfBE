using MassTransit;
using Notification.API.Services.Interfaces;
using Shared.Command;

namespace Notification.API.Consumers
{
    public class GeneralNotificationConsumer : IConsumer<SendGeneralNotificationCommand>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<GeneralNotificationConsumer> _logger;

        public GeneralNotificationConsumer(INotificationService notificationService, ILogger<GeneralNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendGeneralNotificationCommand> context)
        {
            var command = context.Message;
            _logger.LogInformation("Received SendGeneralNotificationCommand for template: {TemplateName}", command.TemplateName);

            try
            {
                await _notificationService.SendGeneralNotificationAsync(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SendGeneralNotificationCommand for template: {TemplateName}", command.TemplateName);
                // Ném lỗi lại để MassTransit có thể xử lý (ví dụ: đưa vào _error queue)
                throw;
            }
        }
    }
}
