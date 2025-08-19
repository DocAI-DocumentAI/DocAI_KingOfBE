using Document.API.Payload.Response;
using Document.API.Constants;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Google Drive storage service interface following existing Azure storage patterns
    /// Implements company-owned folder structure with user delegation
    /// </summary>
    public interface IGoogleDriveService
    {
        /// <summary>
        /// Upload file to Google Drive using company service account
        /// </summary>
        /// <param name="file">File to upload</param>
        /// <param name="folder">Target folder (drafts, pending, approved, archived)</param>
        /// <param name="departmentId">Department ID for access control</param>
        /// <param name="isPublic">Whether document is public or department-restricted</param>
        /// <returns>Upload response with file ID and metadata</returns>
        Task<GoogleDriveUploadResponse> UploadFileAsync(IFormFile file, string folder, string departmentId = null, bool isPublic = false);

        /// <summary>
        /// Delete file from Google Drive
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        Task DeleteFileAsync(string fileId);

        /// <summary>
        /// Move file between folders in Google Drive
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="sourceFolder">Source folder name</param>
        /// <param name="destinationFolder">Destination folder name</param>
        /// <param name="departmentId">Department ID for proper folder placement</param>
        /// <param name="isPublic">Whether document is public</param>
        Task MoveFileAsync(string fileId, string sourceFolder, string destinationFolder, string departmentId = null, bool isPublic = false);

        /// <summary>
        /// Download file from Google Drive
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>File stream</returns>
        Task<Stream> DownloadFileAsync(string fileId);

        /// <summary>
        /// Check if file exists in Google Drive
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>True if file exists</returns>
        Task<bool> FileExistsAsync(string fileId);

        /// <summary>
        /// Get file for viewing with metadata
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>Stream, content type, and filename</returns>
        Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string fileId);

        /// <summary>
        /// Get file content type
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>Content type</returns>
        Task<string> GetFileContentTypeAsync(string fileId);

        /// <summary>
        /// Initialize company folder structure
        /// </summary>
        Task InitializeCompanyFoldersAsync();

        /// <summary>
        /// Get or create a folder and return its ID
        /// </summary>
        /// <param name="folderName">Folder name</param>
        /// <param name="departmentId">Department ID for department-specific folders</param>
        /// <param name="isPublic">Whether the folder is public</param>
        /// <returns>Folder ID</returns>
        Task<string> GetOrCreateFolderAsync(string folderName, string? departmentId, bool isPublic);

        /// <summary>
        /// Grant user access to specific file based on department and public status
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email to grant access</param>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="isPublic">Whether document is public</param>
        /// <param name="role">Permission role (reader, writer)</param>
        Task GrantUserAccessAsync(string fileId, string userEmail, string departmentId, bool isPublic, string role = "reader");

        /// <summary>
        /// Grant user access to a file (simplified overload)
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email address</param>
        /// <param name="role">Permission role (default: reader)</param>
        Task GrantUserAccessAsync(string fileId, string userEmail, string role = "reader");

        /// <summary>
        /// Revoke user access from file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email to revoke access</param>
        Task RevokeUserAccessAsync(string fileId, string userEmail);

        /// <summary>
        /// Get all permissions for a file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>List of permissions</returns>
        Task<IList<Google.Apis.Drive.v3.Data.Permission>> GetFilePermissionsAsync(string fileId);

        /// <summary>
        /// Generate secure iframe viewing URL for Google Drive file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email for access validation</param>
        /// <param name="departmentId">User's department ID for access control</param>
        /// <returns>Iframe URL with access token or null if access denied</returns>
        Task<string?> GenerateIframeViewingUrlAsync(string fileId, string userEmail, string departmentId);

        /// <summary>
        /// Create time-limited sharing link for Google Drive file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email for access validation</param>
        /// <param name="departmentId">User's department ID for access control</param>
        /// <param name="expirationHours">Hours until link expires (default: 24)</param>
        /// <returns>Time-limited sharing URL or null if access denied</returns>
        Task<string?> CreateTimeLimitedSharingLinkAsync(string fileId, string userEmail, string departmentId, int expirationHours = 24);

        /// <summary>
        /// Validate user access to specific file based on department and document status
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>True if user has access</returns>
        Task<bool> ValidateUserAccessAsync(string fileId, string userEmail, string departmentId);

        /// <summary>
        /// Get Google Drive file metadata for iframe viewing
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>File metadata including name, type, and viewing capabilities</returns>
        Task<GoogleDriveFileMetadata> GetFileMetadataForViewingAsync(string fileId);

        /// <summary>
        /// Get iframe viewing URL for a document version with comprehensive access validation
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>Iframe viewing response with URL and metadata</returns>
        Task<ApiResponse<IframeViewingResponse>> GetIframeViewingUrlAsync(string versionId);

        /// <summary>
        /// Get time-limited sharing link for a document version with access validation
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="expirationHours">Hours until link expires (default: 24, max: 168)</param>
        /// <returns>Sharing link response with URL and expiration details</returns>
        Task<ApiResponse<SharingLinkResponse>> GetSharingLinkAsync(string versionId, int expirationHours = 24);

        /// <summary>
        /// Validate user access to a document version with comprehensive metadata
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>File access validation response with detailed access information</returns>
        Task<ApiResponse<FileAccessValidationResponse>> ValidateDocumentAccessAsync(string versionId);

        // ===== NEW FOLDER MANAGEMENT METHODS =====

        /// <summary>
        /// Create a new folder in Google Drive with proper hierarchy
        /// </summary>
        /// <param name="folderName">Name of the folder to create</param>
        /// <param name="parentFolderId">Parent folder ID (null for root level)</param>
        /// <param name="description">Optional folder description</param>
        /// <returns>Created folder ID</returns>
        Task<string> CreateFolderAsync(string folderName, string? parentFolderId = null, string? description = null);

        /// <summary>
        /// Update folder metadata (name, description)
        /// </summary>
        /// <param name="folderId">Folder ID to update</param>
        /// <param name="newName">New folder name (null to keep current)</param>
        /// <param name="newDescription">New description (null to keep current)</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateFolderAsync(string folderId, string? newName = null, string? newDescription = null);

        /// <summary>
        /// Move folder to a different parent
        /// </summary>
        /// <param name="folderId">Folder ID to move</param>
        /// <param name="newParentFolderId">New parent folder ID (null for root)</param>
        /// <returns>Success status</returns>
        Task<bool> MoveFolderAsync(string folderId, string? newParentFolderId);

        /// <summary>
        /// Delete folder from Google Drive
        /// </summary>
        /// <param name="folderId">Folder ID to delete</param>
        /// <param name="force">Force delete even if folder contains files</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteFolderAsync(string folderId, bool force = false);

        /// <summary>
        /// Check if folder exists in Google Drive
        /// </summary>
        /// <param name="folderId">Folder ID to check</param>
        /// <returns>True if folder exists</returns>
        Task<bool> FolderExistsAsync(string folderId);

        /// <summary>
        /// Get folder metadata from Google Drive
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>Folder metadata</returns>
        Task<GoogleDriveFolderMetadata> GetFolderMetadataAsync(string folderId);

        /// <summary>
        /// List contents of a folder (files and subfolders)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeFiles">Include files in results</param>
        /// <param name="includeFolders">Include subfolders in results</param>
        /// <returns>Folder contents</returns>
        Task<GoogleDriveFolderContents> GetFolderContentsAsync(string folderId, bool includeFiles = true, bool includeFolders = true);

        /// <summary>
        /// Upload file to specific folder by folder ID
        /// </summary>
        /// <param name="file">File to upload</param>
        /// <param name="folderId">Target folder ID</param>
        /// <returns>Upload response</returns>
        Task<GoogleDriveUploadResponse> UploadFileToFolderAsync(IFormFile file, string folderId);

        /// <summary>
        /// Delete a folder from Google Drive
        /// </summary>
        /// <param name="folderId">Google Drive folder ID</param>
        /// <returns>Task</returns>
        Task DeleteFolderAsync(string folderId);

        /// <summary>
        /// Move file to specific folder by folder ID
        /// </summary>
        /// <param name="fileId">File ID to move</param>
        /// <param name="targetFolderId">Target folder ID</param>
        /// <returns>Success status</returns>
        Task<bool> MoveFileToFolderAsync(string fileId, string targetFolderId);

        /// <summary>
        /// Grant folder permissions to user or domain
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="emailAddress">User email or domain</param>
        /// <param name="role">Permission role (reader, writer, owner)</param>
        /// <param name="type">Permission type (user, domain)</param>
        /// <returns>Permission ID</returns>
        Task<string> GrantFolderPermissionAsync(string folderId, string emailAddress, string role = "reader", string type = "user");

        /// <summary>
        /// Revoke folder permission
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="permissionId">Permission ID to revoke</param>
        /// <returns>Success status</returns>
        Task<bool> RevokeFolderPermissionAsync(string folderId, string permissionId);

        /// <summary>
        /// Get all permissions for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>List of permissions</returns>
        Task<IList<Google.Apis.Drive.v3.Data.Permission>> GetFolderPermissionsAsync(string folderId);

        /// <summary>
        /// Search for folders by name or path
        /// </summary>
        /// <param name="searchQuery">Search query</param>
        /// <param name="parentFolderId">Parent folder to search within (null for all)</param>
        /// <returns>List of matching folders</returns>
        Task<List<GoogleDriveFolderMetadata>> SearchFoldersAsync(string searchQuery, string? parentFolderId = null);

        /// <summary>
        /// Get folder hierarchy path from root to specified folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>List of folder metadata from root to target</returns>
        Task<List<GoogleDriveFolderMetadata>> GetFolderHierarchyAsync(string folderId);

        /// <summary>
        /// Initialize hierarchical folder structure for department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="departmentName">Department name</param>
        /// <returns>Dictionary of folder type to folder ID mappings</returns>
        Task<Dictionary<string, string>> InitializeDepartmentFolderHierarchyAsync(string departmentId, string departmentName);

        /// <summary>
        /// Initialize public folder hierarchy
        /// </summary>
        /// <returns>Dictionary of folder type to folder ID mappings</returns>
        Task<Dictionary<string, string>> InitializePublicFolderHierarchyAsync();
    }

    /// <summary>
    /// Google Drive file metadata for viewing
    /// </summary>
    public class GoogleDriveFileMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public bool CanViewInBrowser { get; set; }
        public bool RequiresConversion { get; set; }
        public string? ThumbnailLink { get; set; }
        public string? WebViewLink { get; set; }
        public string? WebContentLink { get; set; }
    }

    /// <summary>
    /// Google Drive folder metadata
    /// </summary>
    public class GoogleDriveFolderMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> Parents { get; set; } = new List<string>();
        public DateTime? CreatedTime { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public string? WebViewLink { get; set; }
        public bool Trashed { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
    }

    /// <summary>
    /// Google Drive folder contents
    /// </summary>
    public class GoogleDriveFolderContents
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public List<GoogleDriveFileMetadata> Files { get; set; } = new List<GoogleDriveFileMetadata>();
        public List<GoogleDriveFolderMetadata> Folders { get; set; } = new List<GoogleDriveFolderMetadata>();
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
    }
}
