using System.Security.Claims;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for sending document workflow notifications via MassTransit
    /// </summary>
    public interface IDocumentNotificationService
    {
        /// <summary>
        /// Sends notification when a document is submitted for approval
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="submitterUser">Claims principal of the user who submitted the document</param>
        /// <param name="departmentId">Department ID</param>
        /// <param name="documentLink">Optional link to the document</param>
        Task SendDocumentSubmissionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            ClaimsPrincipal submitterUser,
            string departmentId,
            string? documentLink = null);

        /// <summary>
        /// Sends notification when a document is approved
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="ownerEmail">Email of the document owner</param>
        /// <param name="ownerName">Name of the document owner</param>
        /// <param name="approverUser">Claims principal of the user who approved the document</param>
        /// <param name="comments">Approval comments</param>
        /// <param name="documentLink">Optional link to the document</param>
        Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            ClaimsPrincipal approverUser,
            string? comments = null,
            string? documentLink = null);

        /// <summary>
        /// Sends notification when a document is rejected
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="ownerEmail">Email of the document owner</param>
        /// <param name="ownerName">Name of the document owner</param>
        /// <param name="reviewerUser">Claims principal of the user who rejected the document</param>
        /// <param name="rejectionComments">Rejection comments</param>
        /// <param name="documentLink">Optional link to the document</param>
        Task SendDocumentRejectionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            ClaimsPrincipal reviewerUser,
            string rejectionComments,
            string? documentLink = null);

        /// <summary>
        /// Sends notification to all department users when a document is published
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="approverUser">Claims principal of the user who approved the document</param>
        /// <param name="departmentId">Department ID</param>
        /// <param name="isPublic">Whether the document is public</param>
        /// <param name="documentTypeId">Document type ID</param>
        /// <param name="effectiveFrom">Document effective from date</param>
        /// <param name="effectiveUntil">Document effective until date</param>
        /// <param name="tags">Document tags</param>
        /// <param name="documentLink">Optional link to the document</param>
        Task SendDocumentPublicationNotificationAsync(
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
            string? documentLink = null);
    }
}
