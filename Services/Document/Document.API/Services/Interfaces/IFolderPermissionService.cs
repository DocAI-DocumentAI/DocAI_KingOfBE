using Document.API.Payload.Request.Folder;
using Document.API.Payload.Response.Folder;
using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for advanced folder permission management
    /// Handles complex permission scenarios, inheritance, and bulk operations
    /// </summary>
    public interface IFolderPermissionService
    {
        /// <summary>
        /// Calculate effective permissions for a user on a folder
        /// Considers direct permissions, inherited permissions, and department defaults
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <returns>Effective permission type or null if no access</returns>
        Task<PermissionType?> GetEffectivePermissionAsync(string folderId, string userId, string userDepartmentId);

        /// <summary>
        /// Get detailed permission breakdown for a user on a folder
        /// Shows all permission sources (direct, inherited, department, default)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <returns>Detailed permission breakdown</returns>
        Task<FolderPermissionBreakdownResponse> GetPermissionBreakdownAsync(string folderId, string userId, string userDepartmentId);

        /// <summary>
        /// Bulk set permissions for multiple users/departments on a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="requests">List of permission requests</param>
        /// <param name="applyToSubfolders">Whether to apply to all subfolders</param>
        /// <returns>List of created/updated permissions</returns>
        Task<List<FolderPermissionResponse>> BulkSetPermissionsAsync(string folderId, List<SetFolderPermissionRequest> requests, bool applyToSubfolders = false);

        /// <summary>
        /// Inherit permissions from parent folder
        /// </summary>
        /// <param name="folderId">Child folder ID</param>
        /// <param name="parentFolderId">Parent folder ID</param>
        /// <param name="overrideExisting">Whether to override existing permissions</param>
        /// <returns>Number of permissions inherited</returns>
        Task<int> InheritPermissionsFromParentAsync(string folderId, string parentFolderId, bool overrideExisting = false);

        /// <summary>
        /// Propagate permissions to all subfolders
        /// </summary>
        /// <param name="folderId">Parent folder ID</param>
        /// <param name="permissionType">Permission type to propagate</param>
        /// <param name="targetUserId">Target user ID (null for all users)</param>
        /// <param name="targetDepartmentId">Target department ID (null for all departments)</param>
        /// <returns>Number of subfolders affected</returns>
        Task<int> PropagatePermissionsToSubfoldersAsync(string folderId, PermissionType permissionType, string? targetUserId = null, string? targetDepartmentId = null);

        /// <summary>
        /// Remove all permissions for a user from a folder and its subfolders
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="includeSubfolders">Whether to include subfolders</param>
        /// <returns>Number of permissions removed</returns>
        Task<int> RemoveUserPermissionsAsync(string folderId, string userId, bool includeSubfolders = false);

        /// <summary>
        /// Remove all permissions for a department from a folder and its subfolders
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="departmentId">Department ID</param>
        /// <param name="includeSubfolders">Whether to include subfolders</param>
        /// <returns>Number of permissions removed</returns>
        Task<int> RemoveDepartmentPermissionsAsync(string folderId, string departmentId, bool includeSubfolders = false);

        /// <summary>
        /// Get all folders a user has access to with specific permission level
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="requiredPermission">Required permission level</param>
        /// <param name="departmentId">Filter by department (null for all)</param>
        /// <returns>List of accessible folders</returns>
        Task<List<FolderAccessResponse>> GetUserAccessibleFoldersAsync(string userId, string userDepartmentId, PermissionType requiredPermission, string? departmentId = null);

        /// <summary>
        /// Validate if a user can perform a specific action on a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="action">Action to validate</param>
        /// <returns>Validation result with details</returns>
        Task<PermissionValidationResult> ValidateActionAsync(string folderId, string userId, string userDepartmentId, FolderAction action);

        /// <summary>
        /// Get permission audit trail for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="fromDate">Start date for audit trail</param>
        /// <param name="toDate">End date for audit trail</param>
        /// <returns>List of permission changes</returns>
        Task<List<FolderPermissionAuditResponse>> GetPermissionAuditTrailAsync(string folderId, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Cleanup expired permissions
        /// </summary>
        /// <returns>Number of permissions cleaned up</returns>
        Task<int> CleanupExpiredPermissionsAsync();

        /// <summary>
        /// Get permission conflicts (e.g., user has both allow and deny permissions)
        /// </summary>
        /// <param name="folderId">Folder ID (null for all folders)</param>
        /// <returns>List of permission conflicts</returns>
        Task<List<PermissionConflictResponse>> GetPermissionConflictsAsync(string? folderId = null);

        /// <summary>
        /// Resolve permission conflicts by applying resolution strategy
        /// </summary>
        /// <param name="conflictId">Conflict ID</param>
        /// <param name="resolutionStrategy">How to resolve the conflict</param>
        /// <returns>Resolution result</returns>
        Task<PermissionConflictResolutionResult> ResolvePermissionConflictAsync(string conflictId, ConflictResolutionStrategy resolutionStrategy);

        /// <summary>
        /// Automatically grant folder permissions to a new user for public and department folders
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="userRole">User's role (for determining permission level)</param>
        /// <returns>Number of permissions created</returns>
        Task<int> GrantDefaultFolderPermissionsToNewUserAsync(string userId, string userDepartmentId, string userRole);
    }

    /// <summary>
    /// Folder actions that require permission validation
    /// </summary>
    public enum FolderAction
    {
        View,
        CreateSubfolder,
        UploadDocument,
        EditFolder,
        DeleteFolder,
        ManagePermissions,
        MoveFolder
    }

    /// <summary>
    /// Strategies for resolving permission conflicts
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        AllowWins,      // Remove deny permissions
        DenyWins,       // Remove allow permissions
        MostRecent,     // Keep most recently created permission
        HighestLevel    // Keep permission with highest level
    }
}
