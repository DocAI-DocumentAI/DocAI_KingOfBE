using Microsoft.Extensions.Options;
using Document.API.Configuration;
using Document.API.Services.Interfaces;
using Document.API.Payload.Response;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Unified storage service that can switch between Azure Blob Storage and Google Drive
    /// Provides seamless migration capability
    /// </summary>
    public class UnifiedStorageService : IStorageService
    {
        private readonly IAzureStorageService _azureStorageService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IGoogleDriveOAuthService _googleDriveOAuthService;
        private readonly ILogger<UnifiedStorageService> _logger;
        private readonly StorageConfiguration _config;

        public UnifiedStorageService(
            IAzureStorageService azureStorageService,
            IGoogleDriveService googleDriveService,
            IGoogleDriveOAuthService googleDriveOAuthService,
            IOptions<StorageConfiguration> config,
            ILogger<UnifiedStorageService> logger)
        {
            _azureStorageService = azureStorageService;
            _googleDriveService = googleDriveService;
            _googleDriveOAuthService = googleDriveOAuthService;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<StorageUploadResponse> UploadFileAsync(IFormFile file, string folder, string departmentId = null, bool isPublic = false)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    _logger.LogInformation("Uploading file '{FileName}' to Google Drive", file.FileName);
                    var googleResponse = await _googleDriveService.UploadFileAsync(file, folder, departmentId, isPublic);
                    
                    return new StorageUploadResponse
                    {
                        FileIdentifier = googleResponse.FileId,
                        Md5Hash = googleResponse.Md5Hash,
                        FileName = googleResponse.FileName,
                        FileSize = googleResponse.FileSize,
                        ContentType = googleResponse.ContentType,
                        StorageProvider = "GoogleDrive",
                        UploadedAt = googleResponse.UploadedAt,
                        Metadata = new Dictionary<string, object>
                        {
                            ["FolderId"] = googleResponse.FolderId,
                            ["DownloadUrl"] = googleResponse.DownloadUrl
                        }
                    };
                }
                else
                {
                    _logger.LogInformation("Uploading file '{FileName}' to Azure Blob Storage", file.FileName);
                    var azureResponse = await _azureStorageService.UploadFileAsync(file, folder);
                    
                    return new StorageUploadResponse
                    {
                        FileIdentifier = azureResponse.BlobName,
                        Md5Hash = azureResponse.Md5Hash,
                        FileName = file.FileName,
                        FileSize = file.Length,
                        ContentType = file.ContentType,
                        StorageProvider = "Azure",
                        UploadedAt = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["BlobName"] = azureResponse.BlobName
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{FileName}' using unified storage service", file.FileName);
                throw;
            }
        }

        public async Task DeleteFileAsync(string fileIdentifier, string folder = null)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    await _googleDriveService.DeleteFileAsync(fileIdentifier);
                }
                else
                {
                    // For Azure, we need to extract filename from blob name
                    var filename = ExtractFilenameFromBlobName(fileIdentifier);
                    await _azureStorageService.DeleteFileAsync(filename, folder ?? ExtractFolderFromBlobName(fileIdentifier));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{FileIdentifier}'", fileIdentifier);
                throw;
            }
        }

        public async Task MoveFileAsync(string fileIdentifier, string sourceFolder, string destinationFolder, string departmentId = null, bool isPublic = false)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    await _googleDriveService.MoveFileAsync(fileIdentifier, sourceFolder, destinationFolder, departmentId, isPublic);
                }
                else
                {
                    var filename = ExtractFilenameFromBlobName(fileIdentifier);
                    await _azureStorageService.MoveFileAsync(filename, sourceFolder, destinationFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file '{FileIdentifier}' from '{SourceFolder}' to '{DestinationFolder}'", 
                    fileIdentifier, sourceFolder, destinationFolder);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileIdentifier)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    return await _googleDriveService.DownloadFileAsync(fileIdentifier);
                }
                else
                {
                    return await _azureStorageService.DownloadFileAsync(fileIdentifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file '{FileIdentifier}'", fileIdentifier);
                throw;
            }
        }

        public async Task<bool> FileExistsAsync(string fileIdentifier)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    return await _googleDriveService.FileExistsAsync(fileIdentifier);
                }
                else
                {
                    return await _azureStorageService.FileExistsAsync(fileIdentifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence '{FileIdentifier}'", fileIdentifier);
                return false;
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string fileIdentifier)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    return await _googleDriveService.GetFileForViewingAsync(fileIdentifier);
                }
                else
                {
                    return await _azureStorageService.GetFileForViewingAsync(fileIdentifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file for viewing '{FileIdentifier}'", fileIdentifier);
                throw;
            }
        }

        public async Task<string> GetFileContentTypeAsync(string fileIdentifier)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    return await _googleDriveService.GetFileContentTypeAsync(fileIdentifier);
                }
                else
                {
                    return await _azureStorageService.GetFileContentTypeAsync(fileIdentifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content type '{FileIdentifier}'", fileIdentifier);
                throw;
            }
        }

        public string GetStorageProviderType()
        {
            return _config.UseGoogleDrive ? "GoogleDrive" : "Azure";
        }

        public async Task GrantUserAccessAsync(string fileIdentifier, string userEmail, string departmentId, bool isPublic, string role = "reader")
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    await _googleDriveService.GrantUserAccessAsync(fileIdentifier, userEmail, departmentId, isPublic, role);
                }
                // Azure Blob Storage doesn't support individual file permissions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting user access to file '{FileIdentifier}' for user '{UserEmail}'", 
                    fileIdentifier, userEmail);
                throw;
            }
        }

        public async Task RevokeUserAccessAsync(string fileIdentifier, string userEmail)
        {
            try
            {
                if (_config.UseGoogleDrive && await IsGoogleDriveAvailableAsync())
                {
                    await _googleDriveService.RevokeUserAccessAsync(fileIdentifier, userEmail);
                }
                // Azure Blob Storage doesn't support individual file permissions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking user access to file '{FileIdentifier}' for user '{UserEmail}'", 
                    fileIdentifier, userEmail);
                throw;
            }
        }

        #region Private Helper Methods

        private async Task<bool> IsGoogleDriveAvailableAsync()
        {
            try
            {
                return await _googleDriveOAuthService.HasValidCompanyTokensAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking Google Drive availability, falling back to Azure");
                return false;
            }
        }

        private static string ExtractFilenameFromBlobName(string blobName)
        {
            return Path.GetFileName(blobName);
        }

        private static string ExtractFolderFromBlobName(string blobName)
        {
            var lastSlashIndex = blobName.LastIndexOf('/');
            return lastSlashIndex > 0 ? blobName.Substring(0, lastSlashIndex) : "";
        }

        #endregion
    }

    /// <summary>
    /// Configuration for unified storage service
    /// </summary>
    public class StorageConfiguration
    {
        public const string SectionName = "Storage";

        /// <summary>
        /// Whether to use Google Drive as primary storage
        /// </summary>
        public bool UseGoogleDrive { get; set; } = false;

        /// <summary>
        /// Whether to enable automatic fallback to Azure if Google Drive is unavailable
        /// </summary>
        public bool EnableFallback { get; set; } = true;

        /// <summary>
        /// Whether to enable migration mode (dual storage)
        /// </summary>
        public bool EnableMigrationMode { get; set; } = false;
    }
}
