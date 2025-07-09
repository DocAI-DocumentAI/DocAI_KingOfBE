using MassTransit;
using Notification.API.Command;
using Notification.API.Services.Interfaces;
using Shared.DTOs;

namespace Notification.API.Consumers;

public class ProcessDocumentExpirationConsumer : IConsumer<ProcessDocumentExpirationCommand>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ProcessDocumentExpirationConsumer> _logger;

    public ProcessDocumentExpirationConsumer(INotificationService notificationService, ILogger<ProcessDocumentExpirationConsumer> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessDocumentExpirationCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Consuming expiration command for DocumentId: {DocId}, Type: {Type}",
            command.Document.DocumentId, command.NotificationType);

        try
        {
            if (command.NotificationType == Domain.Enums.NotificationType.Expired)
            {
                await _notificationService.ProcessExpiredDocumentNotification(command.Document);
            }
            else if (command.NotificationType == Domain.Enums.NotificationType.NearingExpiration)
            {
                await _notificationService.ProcessNearingExpirationNotification(command.Document);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}