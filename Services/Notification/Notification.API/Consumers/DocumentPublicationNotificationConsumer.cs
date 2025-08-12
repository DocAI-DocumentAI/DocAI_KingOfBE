using MassTransit;
using Notification.API.Services.Interfaces;
using Notification.API.Payload.Response;
using Notification.API.Utils;
using Shared.Commands;

namespace Notification.API.Consumers
{
    /// <summary>
    /// Consumer for processing document publication notifications
    /// Sends notifications to all department users when a document is published
    /// </summary>
    public class DocumentPublicationNotificationConsumer : IConsumer<DocumentPublicationNotificationCommand>
    {
        private readonly IDocumentWorkflowNotificationService _notificationService;
        private readonly ILogger<DocumentPublicationNotificationConsumer> _logger;

        public DocumentPublicationNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentPublicationNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DocumentPublicationNotificationCommand> context)
        {
            var command = context.Message;
            
            try
            {
                _logger.LogInformation("Processing document publication notification for document {DocumentId}", command.DocumentId);

                var approverInfo = new UserInfo
                {
                    UserId = command.ApproverId.ToString(),
                    Email = command.ApproverEmail,
                    Name = command.ApproverName,
                    Department = command.DepartmentName
                };

                await _notificationService.SendDocumentPublicationNotificationAsync(
                    command.DocumentId,
                    command.DocumentTitle,
                    command.DocumentVersion,
                    approverInfo,
                    command.DepartmentId,
                    command.IsPublic,
                    command.DocumentTypeId,
                    command.DocumentTypeName,
                    command.EffectiveFrom,
                    command.EffectiveUntil,
                    command.Tags,
                    command.DocumentLink);

                _logger.LogInformation("Successfully processed document publication notification for document {DocumentId}", command.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document publication notification for document {DocumentId}", command.DocumentId);
                throw; // Re-throw to trigger retry mechanism
            }
        }
    }
}
