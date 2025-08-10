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
    }
}
