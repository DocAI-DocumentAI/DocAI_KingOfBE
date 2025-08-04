using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Unified storage service interface that abstracts Azure Blob Storage and Google Drive
    /// Allows seamless switching between storage providers during migration
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Upload file to the configured storage provider
        /// </summary>
        /// <param name="file">File to upload</param>
        /// <param name="folder">Target folder</param>
        /// <param name="departmentId">Department ID for access control (Google Drive only)</param>
        /// <param name="isPublic">Whether document is public (Google Drive only)</param>
        /// <returns>Unified upload response</returns>
        Task<StorageUploadResponse> UploadFileAsync(IFormFile file, string folder, string departmentId = null, bool isPublic = false);

        /// <summary>
        /// Delete file from storage
        /// </summary>
        /// <param name="fileIdentifier">File identifier (blob name for Azure, file ID for Google Drive)</param>
        /// <param name="folder">Folder name (Azure only)</param>
        Task DeleteFileAsync(string fileIdentifier, string folder = null);

        /// <summary>
        /// Move file between folders
        /// </summary>
        /// <param name="fileIdentifier">File identifier</param>
        /// <param name="sourceFolder">Source folder</param>
        /// <param name="destinationFolder">Destination folder</param>
        /// <param name="departmentId">Department ID (Google Drive only)</param>
        /// <param name="isPublic">Whether document is public (Google Drive only)</param>
        Task MoveFileAsync(string fileIdentifier, string sourceFolder, string destinationFolder, string departmentId = null, bool isPublic = false);

        /// <summary>
        /// Download file from storage
        /// </summary>
        /// <param name="fileIdentifier">File identifier</param>
        /// <returns>File stream</returns>
        Task<Stream> DownloadFileAsync(string fileIdentifier);
        Task<bool> FileExistsAsync(string fileIdentifier);
        Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string fileIdentifier);
        Task<string> GetFileContentTypeAsync(string fileIdentifier);
        string GetStorageProviderType();
        Task GrantUserAccessAsync(string fileIdentifier, string userEmail, string departmentId, bool isPublic, string role = "reader");
        Task RevokeUserAccessAsync(string fileIdentifier, string userEmail);
    }
}
