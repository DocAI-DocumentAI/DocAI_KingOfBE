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
        private readonly IUserService _userService;

        public DocumentSubmissionNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentSubmissionNotificationConsumer> logger,
            IUserService userService)
        {
            _notificationService = notificationService;
            _logger = logger;
            _userService = userService;
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
                if (string.IsNullOrEmpty(submitterInfo.DepartmentName))
                {
                    _logger.LogWarning("DepartmentName missing, fetching from user service for {UserId}", command.SubmitterId);

                    try
                    {
                        var userInfo = await _userService.GetUserByIdAsync(command.SubmitterId);
                        if (userInfo != null && !string.IsNullOrEmpty(userInfo.DepartmentName))
                        {
                            submitterInfo.DepartmentName = userInfo.DepartmentName;
                            _logger.LogInformation("Retrieved department name: {DepartmentName}", userInfo.DepartmentName);
                        }
                        else
                        {
                            submitterInfo.DepartmentName = "Unknown Department";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to get user info, using fallback");
                        submitterInfo.DepartmentName = "Unknown Department";
                    }
                }
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
        private readonly IUserService _userService;

        public DocumentApprovalNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentApprovalNotificationConsumer> logger,
            IUserService userService)
        {
            _notificationService = notificationService;
            _logger = logger;
            _userService = userService;
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
                try
                {
                    var userInfo = await _userService.GetUserByIdAsync(command.ApproverId);
                    if (userInfo != null)
                    {
                        approverInfo.DepartmentName = userInfo.DepartmentName ?? "Unknown Department";
                        approverInfo.DepartmentId = userInfo.DepartmentId;
                    }
                    else
                    {
                        approverInfo.DepartmentName = "Unknown Department";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get approver info, using fallback");
                    approverInfo.DepartmentName = "Unknown Department";
                }
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
        private readonly IUserService _userService;

        public DocumentRejectionNotificationConsumer(
            IDocumentWorkflowNotificationService notificationService,
            ILogger<DocumentRejectionNotificationConsumer> logger,
            IUserService userService)
        {
            _notificationService = notificationService;
            _logger = logger;
            _userService = userService;
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
                try
                {
                    var userInfo = await _userService.GetUserByIdAsync(command.ReviewerId);
                    if (userInfo != null)
                    {
                        reviewerInfo.DepartmentName = userInfo.DepartmentName ?? "Unknown Department";
                        reviewerInfo.DepartmentId = userInfo.DepartmentId;
                    }
                    else
                    {
                        reviewerInfo.DepartmentName = "Unknown Department";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get reviewer info, using fallback");
                    reviewerInfo.DepartmentName = "Unknown Department";
                }
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
