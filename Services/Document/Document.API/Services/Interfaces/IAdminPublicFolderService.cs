using Document.API.Payload.Request.Admin;
using Document.API.Payload.Response.Admin;
using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for admin management of manager permissions to public folders
    /// </summary>
    public interface IAdminPublicFolderService
    {
        /// <summary>
        /// Grant a manager permission to manage specific public folder(s)
        /// </summary>
        /// <param name="request">Permission grant request</param>
        /// <returns>Granted permission details</returns>
        Task<ManagerPublicFolderPermissionResponse> GrantManagerPermissionAsync(GrantManagerPublicFolderPermissionRequest request);

        /// <summary>
        /// Revoke a manager's permission from specific public folder(s)
        /// </summary>
        /// <param name="request">Permission revoke request</param>
        /// <returns>True if successfully revoked</returns>
        Task<bool> RevokeManagerPermissionAsync(RevokeManagerPublicFolderPermissionRequest request);

        /// <summary>
        /// Grant permissions to multiple managers for public folders
        /// </summary>
        /// <param name="request">Bulk permission grant request</param>
        /// <returns>Bulk operation results</returns>
        Task<BulkManagerPermissionOperationResponse> BulkGrantManagerPermissionsAsync(BulkGrantManagerPermissionsRequest request);

        /// <summary>
        /// Get manager permissions for public folders
        /// </summary>
        /// <param name="request">Get permissions request</param>
        /// <returns>List of manager permissions</returns>
        Task<List<ManagerPublicFolderPermissionResponse>> GetManagerPublicFolderPermissionsAsync(GetManagerPublicFolderPermissionsRequest request);

        /// <summary>
        /// Get summary of a manager's access to public folders
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <returns>Access summary</returns>
        Task<ManagerPublicFolderAccessSummary> GetManagerAccessSummaryAsync(string managerUserId);

        /// <summary>
        /// Get overview of manager access for a specific public folder
        /// </summary>
        /// <param name="publicFolderId">Public folder ID</param>
        /// <returns>Manager access overview</returns>
        Task<PublicFolderManagerAccessOverview> GetPublicFolderManagerAccessOverviewAsync(string publicFolderId);

        /// <summary>
        /// Get all managers who have permissions to any public folders
        /// </summary>
        /// <returns>List of manager access summaries</returns>
        Task<List<ManagerPublicFolderAccessSummary>> GetAllManagersWithPublicFolderAccessAsync();

        /// <summary>
        /// Check if a manager has specific permission to a public folder
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <param name="publicFolderId">Public folder ID</param>
        /// <param name="requiredPermission">Required permission type</param>
        /// <returns>True if manager has the required permission</returns>
        Task<bool> CheckManagerPermissionAsync(string managerUserId, string publicFolderId, PermissionType requiredPermission);

        /// <summary>
        /// Get all public folders that a manager can access with specified permission
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <param name="minimumPermission">Minimum required permission level</param>
        /// <returns>List of accessible public folders</returns>
        Task<List<ManagerPublicFolderPermissionResponse>> GetManagerAccessiblePublicFoldersAsync(string managerUserId, PermissionType minimumPermission = PermissionType.View);

        /// <summary>
        /// Audit trail: Get history of permission changes for a manager or public folder
        /// </summary>
        /// <param name="managerUserId">Manager user ID (optional)</param>
        /// <param name="publicFolderId">Public folder ID (optional)</param>
        /// <param name="fromDate">Start date for audit trail (optional)</param>
        /// <param name="toDate">End date for audit trail (optional)</param>
        /// <returns>List of permission changes</returns>
        Task<List<ManagerPublicFolderPermissionResponse>> GetPermissionAuditTrailAsync(string? managerUserId = null, string? publicFolderId = null, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Validate that a user is actually a manager before granting permissions
        /// </summary>
        /// <param name="userId">User ID to validate</param>
        /// <returns>True if user is a manager</returns>
        Task<bool> ValidateUserIsManagerAsync(string userId);

        /// <summary>
        /// Get all public folders in the system
        /// </summary>
        /// <returns>List of public folders with basic information</returns>
        Task<List<ManagerPublicFolderPermissionResponse>> GetAllPublicFoldersAsync();

        /// <summary>
        /// Cleanup expired permissions
        /// </summary>
        /// <returns>Number of expired permissions cleaned up</returns>
        Task<int> CleanupExpiredPermissionsAsync();
    }
}
