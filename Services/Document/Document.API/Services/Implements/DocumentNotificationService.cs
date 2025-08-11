using Document.API.Services.Interfaces;
using Document.API.Utils;
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
                    SubmitterId = GetUserIdAsGuid(submitterUser),
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
                    ApproverId = GetUserIdAsGuid(approverUser),
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
                    ReviewerId = GetUserIdAsGuid(reviewerUser),
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

        public async Task SendDocumentPublicationNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            ClaimsPrincipal approverUser,
            string departmentId,
            bool isPublic,
            string documentTypeId,
            DateTime? effectiveFrom = null,
            DateTime? effectiveUntil = null,
            List<string>? tags = null,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Sending document publication notification for document {DocumentId} to department {DepartmentId}", documentId, departmentId);

                var command = new DocumentPublicationNotificationCommand
                {
                    DocumentId = documentId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    DepartmentId = departmentId,
                    DepartmentName = GetDepartmentName(approverUser),
                    ApproverId = GetUserIdAsGuid(approverUser),
                    ApproverEmail = GetUserEmail(approverUser),
                    ApproverName = GetUserFullName(approverUser),
                    IsPublic = isPublic,
                    DocumentTypeId = documentTypeId,
                    EffectiveFrom = effectiveFrom,
                    EffectiveUntil = effectiveUntil,
                    Tags = tags ?? new List<string>()
                };

                await _publishEndpoint.Publish(command);
                _logger.LogInformation("Document publication notification sent for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document publication notification for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        #region Helper Methods

        private static Guid GetUserIdAsGuid(ClaimsPrincipal user)
        {
            var userIdString = user?.FindFirst("userId")?.Value;
            if (Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }
            return Guid.Empty; // Return empty Guid if parsing fails
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

        private static string GetDepartmentId(ClaimsPrincipal user)
        {
            return user?.FindFirst("departmentId")?.Value ?? "Unknown";
        }

        #endregion
    }
}