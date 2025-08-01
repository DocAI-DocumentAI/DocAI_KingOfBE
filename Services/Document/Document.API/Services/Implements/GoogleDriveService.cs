using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Document.API.Configuration;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Google Drive storage service implementation for personal Gmail accounts
    /// Uses OAuth2 tokens with company account ownership model
    /// </summary>
    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly GoogleDriveConfiguration _config;
        private readonly ILogger<GoogleDriveService> _logger;
        private readonly IGoogleDriveOAuthService _oauthService;
        private readonly Dictionary<string, string> _folderCache;

        public GoogleDriveService(
            IOptions<GoogleDriveConfiguration> config,
            ILogger<GoogleDriveService> logger,
            IGoogleDriveOAuthService oauthService)
        {
            _config = config.Value;
            _logger = logger;
            _oauthService = oauthService;
            _folderCache = new Dictionary<string, string>();

            _logger.LogInformation("Google Drive service initialized for personal Gmail accounts");
        }

        public async Task<GoogleDriveUploadResponse> UploadFileAsync(IFormFile file, string folder, string departmentId = null, bool isPublic = false)
        {
            try
            {
                _logger.LogInformation("Uploading file '{FileName}' to Google Drive folder '{Folder}'", file.FileName, folder);

                // Use company account for all uploads
                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Get target folder ID
                var folderId = await GetOrCreateFolderAsync(folder, departmentId, isPublic, driveService);

                // Calculate MD5 hash
                var md5Hash = await CalculateMd5HashAsync(file);

                // Create file metadata
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = file.FileName,
                    Parents = new List<string> { folderId },
                    Description = $"Uploaded by DocAI - Department: {departmentId ?? "N/A"}, Public: {isPublic}"
                };

                // Upload file using company account
                using var stream = file.OpenReadStream();
                var request = driveService.Files.Create(fileMetadata, stream, file.ContentType);
                request.Fields = "id,name,size,mimeType,createdTime,md5Checksum,parents";

                var uploadedFile = await ExecuteWithRetryAsync(async () => await request.UploadAsync());

                if (uploadedFile.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    throw new InvalidOperationException($"File upload failed: {uploadedFile.Exception?.Message}");
                }

                var fileResult = request.ResponseBody;

                _logger.LogInformation("File '{FileName}' uploaded successfully with ID '{FileId}'", file.FileName, fileResult.Id);

                return new GoogleDriveUploadResponse
                {
                    FileId = fileResult.Id,
                    Md5Hash = md5Hash,
                    FileName = fileResult.Name,
                    FileSize = fileResult.Size ?? file.Length,
                    ContentType = fileResult.MimeType,
                    FolderId = folderId,
                    UploadedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{FileName}' to Google Drive", file.FileName);
                throw;
            }
        }

        public async Task DeleteFileAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Deleting file with ID '{FileId}' from Google Drive", fileId);

                // Use company account for deletions
                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                await ExecuteWithRetryAsync(async () =>
                {
                    await driveService.Files.Delete(fileId).ExecuteAsync();
                    return true;
                });

                _logger.LogInformation("File '{FileId}' deleted successfully", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{FileId}' from Google Drive", fileId);
                throw;
            }
        }

        public async Task MoveFileAsync(string fileId, string sourceFolder, string destinationFolder, string departmentId = null, bool isPublic = false)
        {
            try
            {
                _logger.LogInformation("Moving file '{FileId}' from '{SourceFolder}' to '{DestinationFolder}'",
                    fileId, sourceFolder, destinationFolder);

                // Use company account for moves
                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Get current file to find current parents
                var fileRequest = driveService.Files.Get(fileId);
                fileRequest.Fields = "id,parents";
                var file = await ExecuteWithRetryAsync(async () => await fileRequest.ExecuteAsync());
                var previousParents = file.Parents != null ? string.Join(",", file.Parents) : "";

                // Get destination folder ID
                var destinationFolderId = await GetOrCreateFolderAsync(destinationFolder, departmentId, isPublic, driveService);

                _logger.LogInformation("Moving file '{FileId}' from parents '{PreviousParents}' to folder '{DestinationFolderId}'",
                    fileId, previousParents, destinationFolderId);

                // Move file
                var updateRequest = driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
                updateRequest.AddParents = destinationFolderId;
                if (!string.IsNullOrEmpty(previousParents))
                {
                    updateRequest.RemoveParents = previousParents;
                }
                updateRequest.Fields = "id,parents";

                await ExecuteWithRetryAsync(async () => await updateRequest.ExecuteAsync());

                _logger.LogInformation("File '{FileId}' moved successfully", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file '{FileId}' from '{SourceFolder}' to '{DestinationFolder}'",
                    fileId, sourceFolder, destinationFolder);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Downloading file with ID '{FileId}' from Google Drive", fileId);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();
                var request = driveService.Files.Get(fileId);
                var stream = new MemoryStream();

                await ExecuteWithRetryAsync(async () =>
                {
                    await request.DownloadAsync(stream);
                    return true;
                });

                stream.Position = 0;
                _logger.LogInformation("File '{FileId}' downloaded successfully", fileId);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file '{FileId}' from Google Drive", fileId);
                throw;
            }
        }

        public async Task<bool> FileExistsAsync(string fileId)
        {
            try
            {
                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();
                var request = driveService.Files.Get(fileId);
                request.Fields = "id";

                await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());
                return true;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file '{FileId}' exists", fileId);
                throw;
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Getting file '{FileId}' for viewing", fileId);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Get file metadata
                var fileRequest = driveService.Files.Get(fileId);
                fileRequest.Fields = "id,name,mimeType,size";
                var fileMetadata = await ExecuteWithRetryAsync(async () => await fileRequest.ExecuteAsync());

                // Download file content
                var stream = await DownloadFileAsync(fileId);

                return (stream, fileMetadata.MimeType, fileMetadata.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file '{FileId}' for viewing", fileId);
                throw;
            }
        }

        public async Task<string> GetFileContentTypeAsync(string fileId)
        {
            try
            {
                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();
                var request = driveService.Files.Get(fileId);
                request.Fields = "mimeType";

                var file = await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());
                return file.MimeType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting content type for file '{FileId}'", fileId);
                throw;
            }
        }

        public async Task InitializeCompanyFoldersAsync()
        {
            try
            {
                _logger.LogInformation("Initializing company folder structure");

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Create root company folder if not exists
                var rootFolderId = await GetOrCreateRootFolderAsync(driveService);

                // Create main workflow folders
                var folders = new[] { "drafts", "pending", "approved", "archived" };
                foreach (var folder in folders)
                {
                    await GetOrCreateFolderAsync(folder, null, false, driveService);
                }

                _logger.LogInformation("Company folder structure initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing company folder structure");
                throw;
            }
        }

        public async Task GrantUserAccessAsync(string fileId, string userEmail, string departmentId, bool isPublic, string role = "reader")
        {
            try
            {
                _logger.LogInformation("Granting {Role} access to file '{FileId}' for user '{UserEmail}'", role, fileId, userEmail);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                var permission = new Permission
                {
                    Type = "user",
                    Role = role,
                    EmailAddress = userEmail
                };

                var request = driveService.Permissions.Create(permission, fileId);
                request.SendNotificationEmail = false;

                await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());

                _logger.LogInformation("Access granted successfully to user '{UserEmail}' for file '{FileId}'", userEmail, fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting access to file '{FileId}' for user '{UserEmail}'", fileId, userEmail);
                throw;
            }
        }

        public async Task RevokeUserAccessAsync(string fileId, string userEmail)
        {
            try
            {
                _logger.LogInformation("Revoking access to file '{FileId}' for user '{UserEmail}'", fileId, userEmail);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Get current permissions
                var permissionsRequest = driveService.Permissions.List(fileId);
                var permissions = await ExecuteWithRetryAsync(async () => await permissionsRequest.ExecuteAsync());

                // Find permission for the user
                var userPermission = permissions.Permissions?.FirstOrDefault(p =>
                    p.EmailAddress?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true);

                if (userPermission != null)
                {
                    await ExecuteWithRetryAsync(async () =>
                    {
                        await driveService.Permissions.Delete(fileId, userPermission.Id).ExecuteAsync();
                        return true;
                    });

                    _logger.LogInformation("Access revoked successfully for user '{UserEmail}' from file '{FileId}'", userEmail, fileId);
                }
                else
                {
                    _logger.LogWarning("No permission found for user '{UserEmail}' on file '{FileId}'", userEmail, fileId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking access to file '{FileId}' for user '{UserEmail}'", fileId, userEmail);
                throw;
            }
        }

        #region Private Helper Methods

        // Removed CreateDriveService - now using OAuth service

        private async Task<string> GetOrCreateRootFolderAsync(DriveService driveService)
        {
            if (!string.IsNullOrEmpty(_config.CompanyRootFolderId))
            {
                return _config.CompanyRootFolderId;
            }

            // Search for existing company root folder
            var searchRequest = driveService.Files.List();
            searchRequest.Q = $"name='{_config.ApplicationName}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
            searchRequest.Fields = "files(id,name)";

            var searchResult = await ExecuteWithRetryAsync(async () => await searchRequest.ExecuteAsync());
            var existingFolder = searchResult.Files?.FirstOrDefault();

            if (existingFolder != null)
            {
                _folderCache["root"] = existingFolder.Id;
                return existingFolder.Id;
            }

            // Create new root folder
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = _config.ApplicationName,
                MimeType = "application/vnd.google-apps.folder",
                Description = "DocAI Company Document Storage"
            };

            var createRequest = driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";

            var createdFolder = await ExecuteWithRetryAsync(async () => await createRequest.ExecuteAsync());
            _folderCache["root"] = createdFolder.Id;

            return createdFolder.Id;
        }

        private async Task<string> GetOrCreateFolderAsync(string folderName, string departmentId = null, bool isPublic = false, DriveService driveService = null)
        {
            var cacheKey = $"{folderName}_{departmentId}_{isPublic}";

            if (_folderCache.TryGetValue(cacheKey, out var cachedFolderId))
            {
                return cachedFolderId;
            }

            driveService ??= await _oauthService.CreateCompanyDriveServiceAsync();
            var rootFolderId = await GetOrCreateRootFolderAsync(driveService);
            var parentFolderId = rootFolderId;

            // For approved and archived folders, create department-specific subfolders
            if ((folderName == "approved" || folderName == "archived") && !isPublic && !string.IsNullOrEmpty(departmentId))
            {
                // First create/get the main folder (approved/archived)
                var mainFolderId = await GetOrCreateSubfolderAsync(folderName, parentFolderId, driveService);

                // Then create/get the department subfolder
                parentFolderId = await GetOrCreateSubfolderAsync(departmentId, mainFolderId, driveService);
            }
            else if ((folderName == "approved" || folderName == "archived") && isPublic)
            {
                // For public documents, create public subfolder
                var mainFolderId = await GetOrCreateSubfolderAsync(folderName, parentFolderId, driveService);
                parentFolderId = await GetOrCreateSubfolderAsync("public", mainFolderId, driveService);
            }
            else
            {
                // For drafts and pending, use main folder directly
                parentFolderId = await GetOrCreateSubfolderAsync(folderName, parentFolderId, driveService);
            }

            _folderCache[cacheKey] = parentFolderId;
            return parentFolderId;
        }

        private async Task<string> GetOrCreateSubfolderAsync(string folderName, string parentFolderId, DriveService driveService)
        {
            // Search for existing folder
            var searchRequest = driveService.Files.List();
            searchRequest.Q = $"name='{folderName}' and '{parentFolderId}' in parents and mimeType='application/vnd.google-apps.folder' and trashed=false";
            searchRequest.Fields = "files(id,name)";

            var searchResult = await ExecuteWithRetryAsync(async () => await searchRequest.ExecuteAsync());
            var existingFolder = searchResult.Files?.FirstOrDefault();

            if (existingFolder != null)
            {
                return existingFolder.Id;
            }

            // Create new folder
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { parentFolderId }
            };

            var createRequest = driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";

            var createdFolder = await ExecuteWithRetryAsync(async () => await createRequest.ExecuteAsync());
            return createdFolder.Id;
        }

        private async Task<string> CalculateMd5HashAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var md5 = MD5.Create();
            var hashBytes = await md5.ComputeHashAsync(stream);
            return Convert.ToBase64String(hashBytes);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            var attempt = 0;
            while (attempt < _config.MaxRetryAttempts)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < _config.MaxRetryAttempts - 1 && IsRetryableException(ex))
                {
                    attempt++;
                    var delay = TimeSpan.FromMilliseconds(_config.BaseDelayMs * Math.Pow(2, attempt - 1));

                    _logger.LogWarning(ex, "Google Drive operation failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms",
                        attempt, _config.MaxRetryAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                }
            }

            // Final attempt without catch
            return await operation();
        }

        private static bool IsRetryableException(Exception ex)
        {
            return ex is GoogleApiException apiEx && (
                apiEx.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                apiEx.HttpStatusCode == System.Net.HttpStatusCode.InternalServerError ||
                apiEx.HttpStatusCode == System.Net.HttpStatusCode.BadGateway ||
                apiEx.HttpStatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                apiEx.HttpStatusCode == System.Net.HttpStatusCode.GatewayTimeout
            );
        }

        #endregion

        public void Dispose()
        {
            // No persistent DriveService to dispose - services are created per operation
        }
    }
}
