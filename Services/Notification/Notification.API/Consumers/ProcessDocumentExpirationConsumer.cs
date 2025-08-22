using MassTransit;
using Notification.API.Command;
using Notification.API.Services.Interfaces;
using Shared.DTOs;

namespace Notification.API.Consumers;

public class ProcessDocumentExpirationConsumer : IConsumer<ProcessDocumentExpirationCommand>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ProcessDocumentExpirationConsumer> _logger;

    public ProcessDocumentExpirationConsumer(
        INotificationService notificationService,
        ILogger<ProcessDocumentExpirationConsumer> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessDocumentExpirationCommand> context)
    {
        var command = context.Message;
        _logger.LogInformation("Processing expiration notification for DocumentId: {DocId}, Type: {Type}",
            command.Document.DocumentId, command.NotificationType);

        try
        {
            switch (command.NotificationType)
            {
                case Domain.Enums.NotificationType.Expired:
                    await _notificationService.ProcessExpiredDocumentNotification(command.Document);
                    break;

                case Domain.Enums.NotificationType.NearingExpiration:
                    await _notificationService.ProcessNearingExpirationNotification(command.Document);
                    break;

                default:
                    _logger.LogWarning("Unknown notification type: {Type} for document {DocId}",
                        command.NotificationType, command.Document.DocumentId);
                    break;
            }

            _logger.LogInformation("Successfully processed {Type} notification for document {DocId}",
                command.NotificationType, command.Document.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Type} notification for document {DocId}",
                command.NotificationType, command.Document.DocumentId);
            throw;
        }
    }
}