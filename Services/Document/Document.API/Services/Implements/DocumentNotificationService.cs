using Document.API.Services.Interfaces;
using MassTransit;
using Shared.Commands;
using System.Security.Claims;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for sending document workflow notifications via MassTransit
    /// </summary>
    public class DocumentNotificationService : IDocumentNotificationService
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<DocumentNotificationService> _logger;

        public DocumentNotificationService(
            IPublishEndpoint publishEndpoint,
            ILogger<DocumentNotificationService> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task SendDocumentSubmissionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            ClaimsPrincipal submitterUser,
            string departmentId,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document submission notification for document {DocumentId}", documentId);

                var command = new DocumentSubmissionNotificationCommand
                {
                    DocumentId = documentId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    SubmitterId = GetUserId(submitterUser),
                    SubmitterEmail = GetUserEmail(submitterUser),
                    SubmitterName = GetUserFullName(submitterUser),
                    DepartmentId = departmentId,
                    DepartmentName = GetDepartmentName(submitterUser)
                };

                await _publishEndpoint.Publish(command);
                _logger.LogInformation("Document submission notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission notification for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        public async Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            ClaimsPrincipal approverUser,
            string? comments = null,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document approval notification for document {DocumentId}", documentId);

                var command = new DocumentApprovalNotificationCommand
                {
                    DocumentId = documentId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    OwnerEmail = ownerEmail,
                    OwnerName = ownerName,
                    ApproverId = GetUserId(approverUser),
                    ApproverEmail = GetUserEmail(approverUser),
                    ApproverName = GetUserFullName(approverUser),
                    Comments = comments
                };

                await _publishEndpoint.Publish(command);
                _logger.LogInformation("Document approval notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document approval notification for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        public async Task SendDocumentRejectionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            ClaimsPrincipal reviewerUser,
            string rejectionComments,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document rejection notification for document {DocumentId}", documentId);

                var command = new DocumentRejectionNotificationCommand
                {
                    DocumentId = documentId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    OwnerEmail = ownerEmail,
                    OwnerName = ownerName,
                    ReviewerId = GetUserId(reviewerUser),
                    ReviewerEmail = GetUserEmail(reviewerUser),
                    ReviewerName = GetUserFullName(reviewerUser),
                    RejectionComments = rejectionComments
                };

                await _publishEndpoint.Publish(command);
                _logger.LogInformation("Document rejection notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document rejection notification for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        #region Helper Methods

        private static string GetUserId(ClaimsPrincipal user)
        {
            return user?.FindFirst("userId")?.Value ?? "Unknown";
        }

        private static string GetUserEmail(ClaimsPrincipal user)
        {
            return user?.FindFirst("email")?.Value ?? "Unknown";
        }

        private static string GetUserFullName(ClaimsPrincipal user)
        {
            return user?.FindFirst("fullName")?.Value ?? "Unknown User";
        }

        private static string GetDepartmentName(ClaimsPrincipal user)
        {
            return user?.FindFirst("departmentName")?.Value ?? "Unknown Department";
        }

        #endregion
    }
}
