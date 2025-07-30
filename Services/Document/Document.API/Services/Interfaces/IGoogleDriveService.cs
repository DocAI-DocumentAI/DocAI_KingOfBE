using Document.API.Payload.Response;

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
        /// Grant user access to specific file based on department and public status
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email to grant access</param>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="isPublic">Whether document is public</param>
        /// <param name="role">Permission role (reader, writer)</param>
        Task GrantUserAccessAsync(string fileId, string userEmail, string departmentId, bool isPublic, string role = "reader");

        /// <summary>
        /// Revoke user access from file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email to revoke access</param>
        Task RevokeUserAccessAsync(string fileId, string userEmail);
    }
}
