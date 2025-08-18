using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// ✅ SIMPLE DEPARTMENT-BASED PERMISSION SERVICE INTERFACE
    /// Implements the simple permission model:
    /// - All department members can VIEW their department's files/folders
    /// - Only specific users get EDIT permissions (managed by managers)
    /// - Managers have full control over their department
    /// </summary>
    public interface ISimpleDepartmentPermissionService
    {
        /// <summary>
        /// Check if user can access folder/document based on simple department rules
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="resourceDepartmentId">Resource (folder/document) department ID</param>
        /// <param name="requiredPermission">Required permission level</param>
        /// <returns>True if user has access</returns>
        Task<bool> CanUserAccessAsync(string userId, string userDepartmentId, string resourceDepartmentId, PermissionType requiredPermission);

        /// <summary>
        /// Grant EDIT permission to a specific user (only managers can do this)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="targetUserId">User to grant permission to</param>
        /// <param name="grantedByUserId">Manager granting the permission</param>
        /// <returns>True if successful</returns>
        Task<bool> GrantEditPermissionAsync(string folderId, string targetUserId, string grantedByUserId);

        /// <summary>
        /// Revoke EDIT permission from a specific user (only managers can do this)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="targetUserId">User to revoke permission from</param>
        /// <param name="revokedByUserId">Manager revoking the permission</param>
        /// <returns>True if successful</returns>
        Task<bool> RevokeEditPermissionAsync(string folderId, string targetUserId, string revokedByUserId);

        /// <summary>
        /// Get all users with EDIT permissions in a folder (for managers to see)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>List of user IDs with edit permissions</returns>
        Task<List<string>> GetUsersWithEditPermissionAsync(string folderId);
    }
}
