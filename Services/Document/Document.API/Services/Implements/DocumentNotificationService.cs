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
            string versionId,
            string documentTitle,
            string documentVersion,
            ClaimsPrincipal submitterUser,
            string departmentId,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Starting document submission notification for document {DocumentId}", documentId);

                // ✅ FIX: Extract user information with enhanced logging for debugging
                var submitterId = GetUserIdAsGuid(submitterUser);
                var submitterEmail = GetUserEmail(submitterUser);
                var submitterName = GetUserFullName(submitterUser);
                var departmentName = GetDepartmentName(submitterUser);

                // ✅ FIX: Log all available claims for debugging
                if (submitterUser?.Claims != null)
                {
                    var allClaims = string.Join(", ", submitterUser.Claims.Select(c => $"{c.Type}: {c.Value}"));
                    _logger.LogInformation("All available claims: {Claims}", allClaims);
                }

                // ✅ FIX: Log extracted user information to verify claims are working
                _logger.LogInformation("Extracted user info for submission notification - ID: {SubmitterId}, Email: {Email}, Name: {Name}, Department: {DepartmentName}", 
                    submitterId, submitterEmail, submitterName, departmentName);

                // Validate required information
                if (submitterId == Guid.Empty)
                {
                    _logger.LogError("Failed to extract valid user ID from claims for document {DocumentId}", documentId);
                    return;
                }

                var command = new DocumentSubmissionNotificationCommand
                {
                    DocumentId = documentId,
                    VersionId = versionId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    SubmitterId = submitterId,
                    SubmitterEmail = submitterEmail,
                    SubmitterName = submitterName,
                    DepartmentId = Guid.Parse(departmentId),
                    DepartmentName = departmentName
                };

                _logger.LogInformation("Publishing document submission notification command for document {DocumentId}", documentId);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _publishEndpoint.Publish(command, cts.Token);
                _logger.LogInformation("Document submission notification command published successfully for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission notification for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        public async Task SendDocumentSubmissionConfirmationAsync(
            string documentId,
            string versionId,
            string documentTitle,
            string documentVersion,
            string submitterEmail,
            string submitterName,
            ClaimsPrincipal submitterUser,
            string? documentLink = null)
        {
            try
            {
                _logger.LogInformation("Starting document submission confirmation for document {DocumentId} to submitter {SubmitterEmail}", documentId, submitterEmail);

                var submitterId = GetUserIdAsGuid(submitterUser);
                var departmentId = GetDepartmentIdAsGuid(submitterUser);
                var departmentName = GetDepartmentName(submitterUser);

                _logger.LogInformation("Extracted user info for submission confirmation - ID: {SubmitterId}, DeptID: {DepartmentId}, DeptName: {DepartmentName}", 
                    submitterId, departmentId, departmentName);

                // Validate required information
                if (submitterId == Guid.Empty)
                {
                    _logger.LogError("Failed to extract valid user ID from claims for submission confirmation {DocumentId}", documentId);
                    return;
                }

                var command = new DocumentSubmissionConfirmationCommand
                {
                    DocumentId = documentId,
                    VersionId = versionId,
                    DocumentTitle = documentTitle,
                    DocumentVersion = documentVersion,
                    DocumentLink = documentLink,
                    SubmitterEmail = submitterEmail,
                    SubmitterName = submitterName,
                    SubmitterId = submitterId,
                    DepartmentId = departmentId,
                    DepartmentName = departmentName
                };

                _logger.LogInformation("Publishing document submission confirmation command for document {DocumentId}", documentId);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _publishEndpoint.Publish(command, cts.Token);
                _logger.LogInformation("Document submission confirmation command published successfully for document {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document submission confirmation for document {DocumentId}", documentId);
                // Don't throw - notification failures shouldn't break the main workflow
            }
        }

        public async Task SendDocumentApprovalNotificationAsync(
            string documentId,
            string versionId,
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
                    VersionId = versionId,
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

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _publishEndpoint.Publish(command, cts.Token);
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
            string versionId,
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
                    VersionId = versionId,
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

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _publishEndpoint.Publish(command, cts.Token);
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
            string versionId,
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
                    VersionId = versionId,
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

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _publishEndpoint.Publish(command, cts.Token);
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
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper - try multiple claim names
            var userIdString = user?.FindFirst("userId")?.Value ??
                              user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                              user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                              
            if (Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }
            return Guid.Empty; // Return empty Guid if parsing fails
        }

        private static string GetUserEmail(ClaimsPrincipal user)
        {
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper
            return user?.FindFirst("email")?.Value ??
                   user?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
                   user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value ??
                   "Unknown";
        }

        private static string GetUserFullName(ClaimsPrincipal user)
        {
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper
            return user?.FindFirst("fullName")?.Value ??
                   user?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ??
                   user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value ??
                   "Unknown User";
        }

        private static string GetDepartmentName(ClaimsPrincipal user)
        {
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper
            return user?.FindFirst("departmentName")?.Value ?? "Unknown Department";
        }

        private static string GetDepartmentId(ClaimsPrincipal user)
        {
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper - try both claim names
            return user?.FindFirst("departmentId")?.Value ?? user?.FindFirst("departmentID")?.Value ?? "Unknown";
        }

        private static Guid GetDepartmentIdAsGuid(ClaimsPrincipal user)
        {
            // ✅ FIX: Use same robust claim extraction as JwtTokenHelper - try both claim names
            var departmentIdString = user?.FindFirst("departmentId")?.Value ?? user?.FindFirst("departmentID")?.Value;
            if (Guid.TryParse(departmentIdString, out var departmentId))
            {
                return departmentId;
            }
            return Guid.Empty; // Return empty Guid if parsing fails
        }

        #endregion
    }
}