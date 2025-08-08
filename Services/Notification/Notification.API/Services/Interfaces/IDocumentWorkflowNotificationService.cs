using Notification.API.Payload.Response;
using Notification.API.Utils;

namespace Notification.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for handling document workflow notifications
    /// </summary>
    public interface IDocumentWorkflowNotificationService
    {
        /// <summary>
        /// Sends notification to department managers when a document is submitted for approval
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="submitterInfo">Information about the user who submitted the document</param>
        /// <param name="departmentId">Department ID for targeting managers</param>
        /// <param name="documentLink">Link to view the document</param>
        Task SendDocumentSubmissionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            UserInfo submitterInfo,
            string departmentId,
            string? documentLink = null);

        /// <summary>
        /// Sends notification to document owner when their document is approved
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="ownerEmail">Email of the document owner</param>
        /// <param name="ownerName">Name of the document owner</param>
        /// <param name="approverInfo">Information about the user who approved the document</param>
        /// <param name="comments">Approval comments</param>
        /// <param name="documentLink">Link to view the document</param>
        Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserInfo approverInfo,
            string? comments = null,
            string? documentLink = null);

        /// <summary>
        /// Sends notification to document owner when their document is rejected
        /// </summary>
        /// <param name="documentId">Document version ID</param>
        /// <param name="documentTitle">Document title</param>
        /// <param name="documentVersion">Document version name</param>
        /// <param name="ownerEmail">Email of the document owner</param>
        /// <param name="ownerName">Name of the document owner</param>
        /// <param name="reviewerInfo">Information about the user who rejected the document</param>
        /// <param name="rejectionComments">Rejection comments/feedback</param>
        /// <param name="documentLink">Link to edit the document</param>
        Task SendDocumentRejectionNotificationAsync(
            string documentId,
            string documentTitle,
            string documentVersion,
            string ownerEmail,
            string ownerName,
            UserInfo reviewerInfo,
            string rejectionComments,
            string? documentLink = null);
    }
}
