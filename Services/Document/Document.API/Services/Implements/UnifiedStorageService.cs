using Microsoft.Extensions.Options;
using Document.API.Configuration;
using Document.API.Services.Interfaces;
using Document.API.Payload.Response;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Unified storage service migrated to Google Drive only
    /// Azure dependencies commented out for migration
    /// </summary>
    public class UnifiedStorageService : IStorageService
    {
        // Azure service commented out for Google Drive migration
        // private readonly IAzureStorageService _azureStorageService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IGoogleDriveOAuthService _googleDriveOAuthService;
        private readonly ILogger<UnifiedStorageService> _logger;
        private readonly StorageConfiguration _config;

        public UnifiedStorageService(
            // IAzureStorageService azureStorageService, // Commented out for migration
            IGoogleDriveService googleDriveService,
            IGoogleDriveOAuthService googleDriveOAuthService,
            IOptions<StorageConfiguration> config,
            ILogger<UnifiedStorageService> logger)
        {
            // _azureStorageService = azureStorageService; // Commented out for migration
            _googleDriveService = googleDriveService;
            _googleDriveOAuthService = googleDriveOAuthService;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<StorageUploadResponse> UploadFileAsync(IFormFile file, string folder, string departmentId = null, bool isPublic = false)
        {
            try
            {
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

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

                // Azure fallback logic commented out for migration
                // else
                // {
                //     _logger.LogInformation("Uploading file '{FileName}' to Azure Blob Storage", file.FileName);
                //     var azureResponse = await _azureStorageService.UploadFileAsync(file, folder);
                //
                //     return new StorageUploadResponse
                //     {
                //         FileIdentifier = azureResponse.BlobName,
                //         Md5Hash = azureResponse.Md5Hash,
                //         FileName = file.FileName,
                //         FileSize = file.Length,
                //         ContentType = file.ContentType,
                //         StorageProvider = "Azure",
                //         UploadedAt = DateTime.UtcNow,
                //         Metadata = new Dictionary<string, object>
                //         {
                //             ["BlobName"] = azureResponse.BlobName
                //         }
                //     };
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{FileName}' using unified storage service", file.FileName);
                throw;
            }
        }

        public async Task<StorageUploadResponse> UploadFileToFolderAsync(IFormFile file, string folderId)
        {
            try
            {
                _logger.LogInformation("Uploading file '{FileName}' to folder ID '{FolderId}'", file.FileName, folderId);

                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                var googleResponse = await _googleDriveService.UploadFileToFolderAsync(file, folderId);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{FileName}' to folder ID '{FolderId}'", file.FileName, folderId);
                throw;
            }
        }

        public async Task DeleteFileAsync(string fileIdentifier, string folder = null)
        {
            try
            {
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                await _googleDriveService.DeleteFileAsync(fileIdentifier);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     // For Azure, we need to extract filename from blob name
                //     var filename = ExtractFilenameFromBlobName(fileIdentifier);
                //     await _azureStorageService.DeleteFileAsync(filename, folder ?? ExtractFolderFromBlobName(fileIdentifier));
                // }
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
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                await _googleDriveService.MoveFileAsync(fileIdentifier, sourceFolder, destinationFolder, departmentId, isPublic);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     var filename = ExtractFilenameFromBlobName(fileIdentifier);
                //     await _azureStorageService.MoveFileAsync(filename, sourceFolder, destinationFolder);
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file '{FileIdentifier}' from '{SourceFolder}' to '{DestinationFolder}'",
                    fileIdentifier, sourceFolder, destinationFolder);
                throw;
            }
        }

        public async Task<bool> MoveFileToFolderAsync(string fileIdentifier, string targetFolderId)
        {
            try
            {
                _logger.LogInformation("Moving file '{FileIdentifier}' to folder ID '{FolderId}'", fileIdentifier, targetFolderId);

                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                return await _googleDriveService.MoveFileToFolderAsync(fileIdentifier, targetFolderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file '{FileIdentifier}' to folder ID '{FolderId}'", fileIdentifier, targetFolderId);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileIdentifier)
        {
            try
            {
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                return await _googleDriveService.DownloadFileAsync(fileIdentifier);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     return await _azureStorageService.DownloadFileAsync(fileIdentifier);
                // }
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
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    _logger.LogWarning("Google Drive is not available for file existence check '{FileIdentifier}'", fileIdentifier);
                    return false;
                }

                return await _googleDriveService.FileExistsAsync(fileIdentifier);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     return await _azureStorageService.FileExistsAsync(fileIdentifier);
                // }
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
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                return await _googleDriveService.GetFileForViewingAsync(fileIdentifier);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     return await _azureStorageService.GetFileForViewingAsync(fileIdentifier);
                // }
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
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                return await _googleDriveService.GetFileContentTypeAsync(fileIdentifier);

                // Azure fallback logic commented out for migration
                // else
                // {
                //     return await _azureStorageService.GetFileContentTypeAsync(fileIdentifier);
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content type '{FileIdentifier}'", fileIdentifier);
                throw;
            }
        }

        public string GetStorageProviderType()
        {
            // Migration complete - always return GoogleDrive
            return "GoogleDrive";
        }

        public async Task GrantUserAccessAsync(string fileIdentifier, string userEmail, string departmentId, bool isPublic, string role = "reader")
        {
            try
            {
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                await _googleDriveService.GrantUserAccessAsync(fileIdentifier, userEmail, departmentId, isPublic, role);

                // Azure Blob Storage doesn't support individual file permissions (commented out for migration)
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
                // Migration complete - using Google Drive only
                if (!await IsGoogleDriveAvailableAsync())
                {
                    throw new InvalidOperationException("Google Drive is not available. Please check authentication and configuration.");
                }

                await _googleDriveService.RevokeUserAccessAsync(fileIdentifier, userEmail);

                // Azure Blob Storage doesn't support individual file permissions (commented out for migration)
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
                _logger.LogError(ex, "Error checking Google Drive availability - migration complete, no Azure fallback");
                return false;
            }
        }

        // Azure helper methods commented out for migration
        // private static string ExtractFilenameFromBlobName(string blobName)
        // {
        //     return Path.GetFileName(blobName);
        // }

        // private static string ExtractFolderFromBlobName(string blobName)
        // {
        //     var lastSlashIndex = blobName.LastIndexOf('/');
        //     return lastSlashIndex > 0 ? blobName.Substring(0, lastSlashIndex) : "";
        // }

        #endregion
    }

    /// <summary>
    /// Configuration for storage service - migrated to Google Drive only
    /// </summary>
    public class StorageConfiguration
    {
        public const string SectionName = "Storage";

        /// <summary>
        /// Whether to use Google Drive as primary storage (migration complete - always true)
        /// </summary>
        public bool UseGoogleDrive { get; set; } = true;

        /// <summary>
        /// Whether to enable automatic fallback to Azure (migration complete - disabled)
        /// </summary>
        public bool EnableFallback { get; set; } = false;

        /// <summary>
        /// Whether to enable migration mode (migration complete - disabled)
        /// </summary>
        public bool EnableMigrationMode { get; set; } = false;
    }
}
