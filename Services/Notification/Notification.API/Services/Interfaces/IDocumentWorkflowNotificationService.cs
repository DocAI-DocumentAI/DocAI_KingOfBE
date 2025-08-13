using Notification.API.Payload.Response;
using Notification.API.Utils;
using Shared.DTOs;

namespace Notification.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for handling document workflow notifications
    /// </summary>
    public interface IDocumentWorkflowNotificationService
    {
        Task SendDocumentSubmissionNotificationAsync(
           string documentId,
           string documentTitle,
           string documentVersion,
           UserDto submitterInfo,
           Guid departmentId,
           string? documentLink = null);

        /// <summary>
        /// Sends confirmation notification to the submitter when a document is submitted for approval
        /// </summary>
        Task SendDocumentSubmissionConfirmationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string submitterEmail,
            string submitterName,
            Guid submitterId,
            string departmentName,
            string? documentLink = null);

        Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserDto approverInfo,
            string? comments = null,
            string? documentLink = null);

        Task SendDocumentRejectionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserDto reviewerInfo,
            string rejectionComments,
            string? documentLink = null);

        /// <summary>
        /// Sends notification to all department users when a document is published
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="approverInfo">Information about the user who approved the document</param>
        /// <param name="departmentId">Department ID for targeting users</param>
        /// <param name="isPublic">Whether the document is public</param>
        /// <param name="documentTypeId">Document type ID</param>
        /// <param name="documentTypeName">Document type name</param>
        /// <param name="effectiveFrom">Document effective from date</param>
        /// <param name="effectiveUntil">Document effective until date</param>
        /// <param name="tags">Document tags</param>
        /// <param name="documentLink">Link to view the document</param>
        Task SendDocumentPublicationNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            UserInfo approverInfo,
            string departmentId,
            bool isPublic,
            string documentTypeId,
            string? documentTypeName = null,
            DateTime? effectiveFrom = null,
            DateTime? effectiveUntil = null,
            List<string>? tags = null,
            string? documentLink = null);
    }
}
