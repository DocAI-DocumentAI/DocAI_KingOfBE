using MassTransit;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Shared.Commands;
using Shared.DTOs;

namespace Notification.API.Consumers
{
    /// <summary>
    /// Consumer for document submission notifications
    /// </summary>
    public class DocumentSubmissionNotificationConsumer : IConsumer<DocumentSubmissionNotificationCommand>
    {
        private readonly IDocumentWorkflowNotificationService _notificationService;
        private readonly ILogger<DocumentSubmissionNotificationConsumer> _logger;

        public DocumentSubmissionNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentSubmissionNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DocumentSubmissionNotificationCommand> context)
        {
            var command = context.Message;
            
            try
            {
                _logger.LogInformation("Processing document submission notification for document {DocumentId}", command.DocumentId);

                var submitterInfo = new UserDto
                {
                    UserId = command.SubmitterId,
                    Email = command.SubmitterEmail,
                    Name = command.SubmitterName,
                    DepartmentId = command.DepartmentId,
                    DepartmentName = command.DepartmentName
                };

                await _notificationService.SendDocumentSubmissionNotificationAsync(
                    command.DocumentId,
                    command.DocumentTitle,
                    command.DocumentVersion,
                    submitterInfo,
                    command.DepartmentId,
                    command.DocumentLink);

                _logger.LogInformation("Successfully processed document submission notification for document {DocumentId}", command.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document submission notification for document {DocumentId}", command.DocumentId);
                throw;
            }
        }
    }

    /// <summary>
    /// Consumer for document approval notifications
    /// </summary>
    public class DocumentApprovalNotificationConsumer : IConsumer<DocumentApprovalNotificationCommand>
    {
        private readonly IDocumentWorkflowNotificationService _notificationService;
        private readonly ILogger<DocumentApprovalNotificationConsumer> _logger;

        public DocumentApprovalNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentApprovalNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DocumentApprovalNotificationCommand> context)
        {
            var command = context.Message;
            
            try
            {
                _logger.LogInformation("Processing document approval notification for document {DocumentId}", command.DocumentId);

                var approverInfo = new UserDto
                {
                    UserId = command.ApproverId,
                    Email = command.ApproverEmail,
                    Name = command.ApproverName
                };

                await _notificationService.SendDocumentApprovalNotificationAsync(
                    command.DocumentId,
                    command.DocumentTitle,
                    command.DocumentVersion,
                    command.OwnerEmail,
                    command.OwnerName,
                    approverInfo,
                    command.Comments,
                    command.DocumentLink);

                _logger.LogInformation("Successfully processed document approval notification for document {DocumentId}", command.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document approval notification for document {DocumentId}", command.DocumentId);
                throw;
            }
        }
    }

    /// <summary>
    /// Consumer for document rejection notifications
    /// </summary>
    public class DocumentRejectionNotificationConsumer : IConsumer<DocumentRejectionNotificationCommand>
    {
        private readonly IDocumentWorkflowNotificationService _notificationService;
        private readonly ILogger<DocumentRejectionNotificationConsumer> _logger;

        public DocumentRejectionNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentRejectionNotificationConsumer> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<DocumentRejectionNotificationCommand> context)
        {
            var command = context.Message;
            
            try
            {
                _logger.LogInformation("Processing document rejection notification for document {DocumentId}", command.DocumentId);

                var reviewerInfo = new UserDto
                {
                    UserId = command.ReviewerId,
                    Email = command.ReviewerEmail,
                    Name = command.ReviewerName
                };

                await _notificationService.SendDocumentRejectionNotificationAsync(
                    command.DocumentId,
                    command.DocumentTitle,
                    command.DocumentVersion,
                    command.OwnerEmail,
                    command.OwnerName,
                    reviewerInfo,
                    command.RejectionComments,
                    command.DocumentLink);

                _logger.LogInformation("Successfully processed document rejection notification for document {DocumentId}", command.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document rejection notification for document {DocumentId}", command.DocumentId);
                throw;
            }
        }
    }
}
