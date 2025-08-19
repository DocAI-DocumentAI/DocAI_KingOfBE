using Document.API.Models;
using Document.API.Payload.Request.Folder;
using Document.API.Payload.Response.Folder;
using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for folder management operations
    /// Provides hierarchical folder structure similar to Google Drive
    /// </summary>
    public interface IFolderService
    {
        /// <summary>
        /// Get folder tree structure for a department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="includeSystemFolders">Include system folders (_approved, _draft, etc.)</param>
        /// <param name="maxDepth">Maximum depth to retrieve (null for all levels)</param>
        /// <returns>Hierarchical folder tree</returns>
        Task<FolderTreeResponse> GetFolderTreeAsync(string departmentId, bool includeSystemFolders = true, int? maxDepth = null);

        /// <summary>
        /// Get public folder tree accessible to all employees
        /// </summary>
        /// <param name="includeSystemFolders">Include system folders</param>
        /// <param name="maxDepth">Maximum depth to retrieve</param>
        /// <returns>Public folder tree</returns>
        Task<FolderTreeResponse> GetPublicFolderTreeAsync(bool includeSystemFolders = true, int? maxDepth = null);

        /// <summary>
        /// Get folder details by ID
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>Folder details</returns>
        Task<FolderDetailResponse> GetFolderByIdAsync(string folderId);

        /// <summary>
        /// Get folder by path
        /// </summary>
        /// <param name="fullPath">Full folder path</param>
        /// <param name="departmentId">Department ID (null for public folders)</param>
        /// <returns>Folder details</returns>
        Task<FolderDetailResponse> GetFolderByPathAsync(string fullPath, string? departmentId = null);

        /// <summary>
        /// Create a new folder
        /// </summary>
        /// <param name="request">Folder creation request</param>
        /// <returns>Created folder details</returns>
        Task<FolderDetailResponse> CreateFolderAsync(CreateFolderRequest request);

        /// <summary>
        /// Update folder information
        /// </summary>
        /// <param name="folderId">Folder ID to update</param>
        /// <param name="request">Update request</param>
        /// <returns>Updated folder details</returns>
        Task<FolderDetailResponse> UpdateFolderAsync(string folderId, UpdateFolderRequest request);

        /// <summary>
        /// Move folder to a different parent
        /// </summary>
        /// <param name="folderId">Folder ID to move</param>
        /// <param name="request">Move request with new parent</param>
        /// <returns>Updated folder details</returns>
        Task<FolderDetailResponse> MoveFolderAsync(string folderId, MoveFolderRequest request);

        /// <summary>
        /// Delete folder (soft delete)
        /// </summary>
        /// <param name="folderId">Folder ID to delete</param>
        /// <param name="force">Force delete even if folder contains items</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteFolderAsync(string folderId, bool force = false);

        /// <summary>
        /// Get folders that user has access to
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="permissionType">Minimum permission level required</param>
        /// <returns>List of accessible folders</returns>
        Task<List<FolderSummaryResponse>> GetAccessibleFoldersAsync(string userId, string departmentId, PermissionType permissionType = PermissionType.View);

        /// <summary>
        /// Check if user has permission to access folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID</param>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="requiredPermission">Required permission level</param>
        /// <returns>True if user has permission</returns>
        Task<bool> HasFolderPermissionAsync(string folderId, string userId, string departmentId, PermissionType requiredPermission);

        /// <summary>
        /// Get folder permissions
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>List of folder permissions</returns>
        Task<List<FolderPermissionResponse>> GetFolderPermissionsAsync(string folderId);

        /// <summary>
        /// Set folder permission for user or department
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="request">Permission request</param>
        /// <returns>Created permission details</returns>
        Task<FolderPermissionResponse> SetFolderPermissionAsync(string folderId, SetFolderPermissionRequest request);

        /// <summary>
        /// Remove folder permission
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="permissionId">Permission ID to remove</param>
        /// <returns>Success status</returns>
        Task<bool> RemoveFolderPermissionAsync(string folderId, string permissionId);

        /// <summary>
        /// Initialize system folders for a department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="departmentName">Department name</param>
        /// <returns>List of created folder IDs</returns>
        Task<List<string>> InitializeDepartmentFoldersAsync(string departmentId, string departmentName);

        /// <summary>
        /// Initialize public system folders
        /// </summary>
        /// <returns>List of created folder IDs</returns>
        Task<List<string>> InitializePublicFoldersAsync();

        /// <summary>
        /// Verify synchronization between database and Google Drive folders
        /// </summary>
        /// <param name="departmentId">Optional department ID to check specific department</param>
        /// <returns>Sync verification result</returns>
        Task<FolderSyncVerificationResult> VerifyFolderSyncAsync(string? departmentId = null);

        /// <summary>
        /// Get folder breadcrumb path
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>Breadcrumb path from root to folder</returns>
        Task<List<FolderBreadcrumbResponse>> GetFolderBreadcrumbAsync(string folderId);

        /// <summary>
        /// Search folders by name or path
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <param name="departmentId">Department ID (null for public folders)</param>
        /// <param name="userId">User ID for permission filtering</param>
        /// <returns>List of matching folders</returns>
        Task<List<FolderSummaryResponse>> SearchFoldersAsync(string searchTerm, string? departmentId, string userId);

        /// <summary>
        /// Validate folder name and path
        /// </summary>
        /// <param name="folderName">Folder name to validate</param>
        /// <param name="parentFolderId">Parent folder ID</param>
        /// <returns>Validation result</returns>
        Task<FolderValidationResult> ValidateFolderNameAsync(string folderName, string? parentFolderId);
    }
}
