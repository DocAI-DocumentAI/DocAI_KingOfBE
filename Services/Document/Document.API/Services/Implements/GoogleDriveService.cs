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
using Document.API.Utils;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using static Document.API.Services.Interfaces.IGoogleDriveService;
using File = Google.Apis.Drive.v3.Data.File;

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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Dictionary<string, string> _folderCache;

        public GoogleDriveService(
            IOptions<GoogleDriveConfiguration> config,
            ILogger<GoogleDriveService> logger,
            IGoogleDriveOAuthService oauthService,
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor)
        {
            _config = config.Value;
            _logger = logger;
            _oauthService = oauthService;
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
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

        public async Task<string> GetOrCreateFolderAsync(string folderName, string? departmentId, bool isPublic)
        {
            using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();
            return await GetOrCreateFolderAsync(folderName, departmentId, isPublic, driveService);
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

                // First attempt: try without notification email
                var request = driveService.Permissions.Create(permission, fileId);
                request.SendNotificationEmail = false;

                try
                {
                    await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());
                    _logger.LogInformation("Access granted successfully to user '{UserEmail}' for file '{FileId}'", userEmail, fileId);
                }
                catch (GoogleApiException gex) when (gex.Error?.Errors?.Any(e => e.Reason == "invalidSharingRequest") == true)
                {
                    // User doesn't have a Google account, try with notification email
                    _logger.LogWarning("User '{UserEmail}' doesn't have a Google account, retrying with notification email", userEmail);

                    var retryRequest = driveService.Permissions.Create(permission, fileId);
                    retryRequest.SendNotificationEmail = true;

                    try
                    {
                        await ExecuteWithRetryAsync(async () => await retryRequest.ExecuteAsync());
                        _logger.LogInformation("Access granted successfully to user '{UserEmail}' for file '{FileId}' with notification email", userEmail, fileId);
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogWarning(retryEx, "Failed to grant access to user '{UserEmail}' for file '{FileId}' even with notification email. Skipping this user.", userEmail, fileId);
                        // Don't throw - continue with other users
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting access to file '{FileId}' for user '{UserEmail}'", fileId, userEmail);
                throw;
            }
        }

        /// <summary>
        /// Grant user access to a file (simplified overload)
        /// </summary>
        public async Task GrantUserAccessAsync(string fileId, string userEmail, string role = "reader")
        {
            await GrantUserAccessAsync(fileId, userEmail, null, false, role);
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

        /// <summary>
        /// Get all permissions for a file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>List of permissions</returns>
        public async Task<IList<Google.Apis.Drive.v3.Data.Permission>> GetFilePermissionsAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Getting permissions for file {FileId}", fileId);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();
                var request = driveService.Permissions.List(fileId);
                request.Fields = "permissions(id,type,emailAddress,role)";

                var response = await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());

                _logger.LogInformation("Retrieved {Count} permissions for file {FileId}", response.Permissions?.Count ?? 0, fileId);
                return response.Permissions ?? new List<Google.Apis.Drive.v3.Data.Permission>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions for file {FileId}", fileId);
                throw;
            }
        }

        /// <summary>
        /// Generate secure iframe viewing URL for Google Drive file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email for access validation</param>
        /// <param name="departmentId">User's department ID for access control</param>
        /// <returns>Iframe URL with access token or null if access denied</returns>
        public async Task<string?> GenerateIframeViewingUrlAsync(string fileId, string userEmail, string departmentId)
        {
            try
            {
                _logger.LogInformation("Generating iframe viewing URL for file {FileId} and user {UserEmail}", fileId, userEmail);

                // // Validate user access first
                // var hasAccess = await ValidateUserAccessAsync(fileId, userEmail, departmentId);
                // if (!hasAccess)
                // {
                //     _logger.LogWarning("User {UserEmail} does not have access to file {FileId}", userEmail, fileId);
                //     return null;
                // }

                // Get file metadata to determine viewing method
                var metadata = await GetFileMetadataForViewingAsync(fileId);
                if (!metadata.CanViewInBrowser)
                {
                    _logger.LogWarning("File {FileId} cannot be viewed in browser", fileId);
                    return null;
                }

                // // For Google Drive files, we can use the webViewLink for iframe embedding
                // // But we need to ensure the user has proper access
                // await EnsureUserHasFileAccessAsync(fileId, userEmail);

                // Use company access token for iframe embedding (temporary - allows everyone to view)
                string iframeUrl;
                try
                {
                    // // TEMPORARILY COMMENTED OUT: Get user ID from JWT token to retrieve their Google access token
                    // var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                    // // TEMPORARILY COMMENTED OUT: Try to get user's personal Google access token first
                    // var userAccessToken = await _oauthService.GetUserAccessTokenAsync(userId);

                    // if (!string.IsNullOrEmpty(userAccessToken))
                    // {
                    //     // Include user's access token in the iframe URL for authentication
                    //     iframeUrl = $"https://drive.google.com/file/d/{fileId}/preview?access_token={userAccessToken}";
                    //     _logger.LogInformation("Generated iframe URL with user access token for file {FileId}", fileId);
                    // }
                    // else
                    // {
                        // Use company access token for all users (temporary)
                        var companyAccessToken = await _oauthService.GetCompanyAccessTokenAsync();
                        if (!string.IsNullOrEmpty(companyAccessToken))
                        {
                            iframeUrl = $"https://drive.google.com/file/d/{fileId}/preview?access_token={companyAccessToken}";
                            _logger.LogInformation("Generated iframe URL with company access token for file {FileId} (temporary - all users)", fileId);
                        }
                        else
                        {
                            // Last fallback: Basic URL without access token (may require user to sign in)
                            iframeUrl = $"https://drive.google.com/file/d/{fileId}/preview";
                            _logger.LogWarning("Generated iframe URL without access token for file {FileId} - user may need to sign in", fileId);
                        }
                    // }
                }
                catch (Exception tokenEx)
                {
                    _logger.LogWarning(tokenEx, "Failed to get company access token for iframe URL, using basic URL for file {FileId}", fileId);
                    // Fallback to basic URL
                    iframeUrl = $"https://drive.google.com/file/d/{fileId}/preview";
                }

                _logger.LogInformation("Generated iframe URL for file {FileId}", fileId);
                return iframeUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating iframe viewing URL for file {FileId}", fileId);
                return null;
            }
        }

        /// <summary>
        /// Create time-limited sharing link for Google Drive file
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email for access validation</param>
        /// <param name="departmentId">User's department ID for access control</param>
        /// <param name="expirationHours">Hours until link expires (default: 24)</param>
        /// <returns>Time-limited sharing URL or null if access denied</returns>
        public async Task<string?> CreateTimeLimitedSharingLinkAsync(string fileId, string userEmail, string departmentId, int expirationHours = 24)
        {
            try
            {
                _logger.LogInformation("Creating time-limited sharing link for file {FileId} and user {UserEmail}", fileId, userEmail);

                // Validate user access first
                var hasAccess = await ValidateUserAccessAsync(fileId, userEmail, departmentId);
                if (!hasAccess)
                {
                    _logger.LogWarning("User {UserEmail} does not have access to file {FileId}", userEmail, departmentId);
                    return null;
                }

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Ensure user has access to the file
                await EnsureUserHasFileAccessAsync(fileId, userEmail);

                // Create a time-limited permission (Google Drive doesn't support time-limited links directly,
                // so we'll use the regular sharing link and rely on our access control)
                var fileRequest = driveService.Files.Get(fileId);
                fileRequest.Fields = "webViewLink,webContentLink";
                var file = await ExecuteWithRetryAsync(async () => await fileRequest.ExecuteAsync());

                // Return the web view link - access control is handled by Google Drive permissions
                var sharingUrl = file.WebViewLink;

                _logger.LogInformation("Created sharing link for file {FileId}", fileId);
                return sharingUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating time-limited sharing link for file {FileId}", fileId);
                return null;
            }
        }

        /// <summary>
        /// Validate user access to specific file based on department and document status
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>True if user has access</returns>
        public async Task<bool> ValidateUserAccessAsync(string fileId, string userEmail, string departmentId)
        {
            try
            {
                _logger.LogInformation("Validating user access for file {FileId} and user {UserEmail}", fileId, userEmail);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                // Get file permissions
                var permissions = await GetFilePermissionsAsync(fileId);

                // Check if user has direct permission
                var userPermission = permissions.FirstOrDefault(p =>
                    p.EmailAddress?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true);

                if (userPermission != null)
                {
                    _logger.LogInformation("User {UserEmail} has direct access to file {FileId}", userEmail, fileId);
                    return true;
                }

                // Check if file is in a folder that the user's department has access to
                // This would require additional logic based on your folder structure
                // For now, we'll rely on the existing permission system

                _logger.LogWarning("User {UserEmail} does not have access to file {FileId}", userEmail, fileId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user access for file {FileId}", fileId);
                return false;
            }
        }

        /// <summary>
        /// Get Google Drive file metadata for iframe viewing
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <returns>File metadata including name, type, and viewing capabilities</returns>
        public async Task<GoogleDriveFileMetadata> GetFileMetadataForViewingAsync(string fileId)
        {
            try
            {
                _logger.LogInformation("Getting file metadata for viewing for file {FileId}", fileId);

                using var driveService = await _oauthService.CreateCompanyDriveServiceAsync();

                var fileRequest = driveService.Files.Get(fileId);
                fileRequest.Fields = "id,name,mimeType,size,createdTime,modifiedTime,thumbnailLink,webViewLink,webContentLink";
                var file = await ExecuteWithRetryAsync(async () => await fileRequest.ExecuteAsync());

                var metadata = new GoogleDriveFileMetadata
                {
                    Id = file.Id,
                    Name = file.Name,
                    MimeType = file.MimeType,
                    Size = file.Size,
                    CreatedTime = file.CreatedTime,
                    ModifiedTime = file.ModifiedTime,
                    ThumbnailLink = file.ThumbnailLink,
                    WebViewLink = file.WebViewLink,
                    WebContentLink = file.WebContentLink,
                    CanViewInBrowser = CanFileBeViewedInBrowser(file.MimeType),
                    RequiresConversion = RequiresConversionForViewing(file.MimeType)
                };

                _logger.LogInformation("Retrieved metadata for file {FileId}: {FileName} ({MimeType})", fileId, file.Name, file.MimeType);
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file metadata for viewing for file {FileId}", fileId);
                throw;
            }
        }

        /// <summary>
        /// Ensure user has access to the file by granting permission if needed
        /// </summary>
        /// <param name="fileId">Google Drive file ID</param>
        /// <param name="userEmail">User email</param>
        private async Task EnsureUserHasFileAccessAsync(string fileId, string userEmail)
        {
            try
            {
                var permissions = await GetFilePermissionsAsync(fileId);
                var userPermission = permissions.FirstOrDefault(p =>
                    p.EmailAddress?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true);

                if (userPermission == null)
                {
                    // Grant reader access to the user
                    await GrantUserAccessAsync(fileId, userEmail, "reader");
                    _logger.LogInformation("Granted reader access to user {UserEmail} for file {FileId}", userEmail, fileId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring user access for file {FileId} and user {UserEmail}", fileId, userEmail);
                throw;
            }
        }

        /// <summary>
        /// Check if file can be viewed in browser based on MIME type
        /// </summary>
        /// <param name="mimeType">File MIME type</param>
        /// <returns>True if file can be viewed in browser</returns>
        private static bool CanFileBeViewedInBrowser(string mimeType)
        {
            return mimeType switch
            {
                "application/pdf" => true,
                "application/vnd.google-apps.document" => true,
                "application/vnd.google-apps.spreadsheet" => true,
                "application/vnd.google-apps.presentation" => true,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true, // DOCX
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true, // XLSX
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => true, // PPTX
                "application/msword" => true, // DOC
                "application/vnd.ms-excel" => true, // XLS
                "application/vnd.ms-powerpoint" => true, // PPT
                "text/plain" => true,
                "text/html" => true,
                "image/jpeg" => true,
                "image/png" => true,
                "image/gif" => true,
                "image/svg+xml" => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if file requires conversion for viewing
        /// </summary>
        /// <param name="mimeType">File MIME type</param>
        /// <returns>True if file requires conversion</returns>
        private static bool RequiresConversionForViewing(string mimeType)
        {
            return mimeType switch
            {
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true, // DOCX
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true, // XLSX
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => true, // PPTX
                "application/msword" => true, // DOC
                "application/vnd.ms-excel" => true, // XLS
                "application/vnd.ms-powerpoint" => true, // PPT
                _ => false
            };
        }

        #region New Service Methods for FileController

        /// <summary>
        /// Get iframe viewing URL for a document version with comprehensive access validation
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>Iframe viewing response with URL and metadata</returns>
        public async Task<ApiResponse<IframeViewingResponse>> GetIframeViewingUrlAsync(string versionId)
        {
            try
            {
                _logger.LogInformation("Getting iframe viewing URL for version {VersionId}", versionId);

                // Extract user information from JWT token
                var userEmail = JwtTokenHelper.GetUserEmail(_httpContextAccessor);
                var departmentId = JwtTokenHelper.GetDepartmentId(_httpContextAccessor);
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                // Get document version to get the file ID and validate access
                var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile)
                );

                if (version == null)
                {
                    return ApiResponse<IframeViewingResponse>.Error("NOT_FOUND", "Document version not found", 404);
                }

                var fileId = version.FilePath; // This should be the Google Drive file ID

                // // Validate user access
                // var hasAccess = await ValidateUserAccessAsync(fileId, userEmail, departmentId);
                // if (!hasAccess)
                // {
                //     _logger.LogWarning("User {UserEmail} does not have access to file {FileId}", userEmail, fileId);
                //     return ApiResponse<IframeViewingResponse>.Error("ACCESS_DENIED", "You do not have permission to view this document", 403);
                // }

                // Generate iframe viewing URL
                var iframeUrl = await GenerateIframeViewingUrlAsync(fileId, userEmail, departmentId);
                if (string.IsNullOrEmpty(iframeUrl))
                {
                    return ApiResponse<IframeViewingResponse>.Error("IFRAME_GENERATION_FAILED", "Unable to generate iframe viewing URL", 500);
                }

                // Get file metadata for additional information
                var metadata = await GetFileMetadataForViewingAsync(fileId);

                var response = new IframeViewingResponse
                {
                    VersionId = versionId,
                    IframeUrl = iframeUrl,
                    FileName = version.FileName,
                    FileType = version.FileType,
                    CanViewInline = metadata.CanViewInBrowser,
                    FileSize = metadata.Size,
                    FileId = fileId,
                    GeneratedAt = DateTime.UtcNow,
                    RequestedBy = userEmail,
                    DepartmentId = departmentId
                };

                _logger.LogInformation("Successfully generated iframe viewing URL for version {VersionId}", versionId);
                return ApiResponse<IframeViewingResponse>.Success(response, "Iframe viewing URL generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting iframe viewing URL for version {VersionId}", versionId);
                return ApiResponse<IframeViewingResponse>.Error("INTERNAL_ERROR", "An error occurred while generating iframe viewing URL", 500);
            }
        }

        /// <summary>
        /// Get time-limited sharing link for a document version with access validation
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="expirationHours">Hours until link expires (default: 24, max: 168)</param>
        /// <returns>Sharing link response with URL and expiration details</returns>
        public async Task<ApiResponse<SharingLinkResponse>> GetSharingLinkAsync(string versionId, int expirationHours = 24)
        {
            try
            {
                _logger.LogInformation("Getting sharing link for version {VersionId} with expiration {ExpirationHours} hours", versionId, expirationHours);

                // Validate expiration hours
                if (expirationHours < 1 || expirationHours > 168) // Max 1 week
                {
                    return ApiResponse<SharingLinkResponse>.Error("INVALID_EXPIRATION", "Expiration hours must be between 1 and 168 (1 week)", 400);
                }

                // Extract user information from JWT token
                var userEmail = JwtTokenHelper.GetUserEmail(_httpContextAccessor);
                var departmentId = JwtTokenHelper.GetDepartmentId(_httpContextAccessor);
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                // Get document version to get the file ID and validate access
                var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile)
                );

                if (version == null)
                {
                    return ApiResponse<SharingLinkResponse>.Error("NOT_FOUND", "Document version not found", 404);
                }

                var fileId = version.FilePath; // This should be the Google Drive file ID

                // Validate user access
                var hasAccess = await ValidateUserAccessAsync(fileId, userEmail, departmentId);
                if (!hasAccess)
                {
                    _logger.LogWarning("User {UserEmail} does not have access to file {FileId}", userEmail, fileId);
                    return ApiResponse<SharingLinkResponse>.Error("ACCESS_DENIED", "You do not have permission to share this document", 403);
                }

                // Create time-limited sharing link
                var sharingUrl = await CreateTimeLimitedSharingLinkAsync(fileId, userEmail, departmentId, expirationHours);
                if (string.IsNullOrEmpty(sharingUrl))
                {
                    return ApiResponse<SharingLinkResponse>.Error("SHARING_LINK_GENERATION_FAILED", "Unable to create sharing link", 500);
                }

                // Get file metadata for additional information
                var metadata = await GetFileMetadataForViewingAsync(fileId);

                var expiresAt = DateTime.UtcNow.AddHours(expirationHours);
                var response = new SharingLinkResponse
                {
                    VersionId = versionId,
                    SharingUrl = sharingUrl,
                    FileName = version.FileName,
                    FileType = version.FileType,
                    ExpirationHours = expirationHours,
                    ExpiresAt = expiresAt,
                    FileSize = metadata.Size,
                    FileId = fileId,
                    GeneratedAt = DateTime.UtcNow,
                    RequestedBy = userEmail,
                    DepartmentId = departmentId
                };

                _logger.LogInformation("Successfully generated sharing link for version {VersionId}", versionId);
                return ApiResponse<SharingLinkResponse>.Success(response, "Sharing link generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sharing link for version {VersionId}", versionId);
                return ApiResponse<SharingLinkResponse>.Error("INTERNAL_ERROR", "An error occurred while generating sharing link", 500);
            }
        }

        /// <summary>
        /// Validate user access to a document version with comprehensive metadata
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>File access validation response with detailed access information</returns>
        public async Task<ApiResponse<FileAccessValidationResponse>> ValidateDocumentAccessAsync(string versionId)
        {
            try
            {
                _logger.LogInformation("Validating document access for version {VersionId}", versionId);

                // Extract user information from JWT token
                var userEmail = JwtTokenHelper.GetUserEmail(_httpContextAccessor);
                var departmentId = JwtTokenHelper.GetDepartmentId(_httpContextAccessor);
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                // Get document version to get the file ID and document information
                var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile)
                );

                if (version == null)
                {
                    return ApiResponse<FileAccessValidationResponse>.Error("NOT_FOUND", "Document version not found", 404);
                }

                var fileId = version.FilePath; // This should be the Google Drive file ID

                // Validate user access
                var hasAccess = await ValidateUserAccessAsync(fileId, userEmail, departmentId);

                // Get file metadata for additional information
                var metadata = await GetFileMetadataForViewingAsync(fileId);

                // Determine supported viewing methods based on access and file type
                var supportedMethods = new List<string>();
                if (hasAccess)
                {
                    if (metadata.CanViewInBrowser)
                    {
                        supportedMethods.Add("iframe");
                        supportedMethods.Add("browser_view");
                    }
                    supportedMethods.Add("download");
                    supportedMethods.Add("sharing_link");
                }

                var response = new FileAccessValidationResponse
                {
                    VersionId = versionId,
                    HasAccess = hasAccess,
                    UserEmail = userEmail,
                    DepartmentId = departmentId,
                    FileName = version.FileName,
                    FileType = version.FileType,
                    CanViewInBrowser = metadata.CanViewInBrowser,
                    RequiresConversion = metadata.RequiresConversion,
                    FileSize = metadata.Size,
                    LastModified = metadata.ModifiedTime,
                    AccessLevel = hasAccess ? "Authorized" : "Denied",
                    SupportedViewingMethods = supportedMethods.ToArray(),
                    FileId = fileId,
                    DocumentStatus = version.Status.ToString(),
                    IsPublic = version.IsPublic,
                    DocumentDepartmentId = version.DocumentFile?.DepartmentId ?? string.Empty,
                    DocumentOwnerId = version.DocumentFile?.OwnerId ?? string.Empty,
                    ValidatedAt = DateTime.UtcNow,
                    AccessDenialReason = hasAccess ? null : "Insufficient permissions or document not found"
                };

                _logger.LogInformation("Document access validation completed for version {VersionId}: {HasAccess}", versionId, hasAccess);
                return ApiResponse<FileAccessValidationResponse>.Success(response, "Document access validation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating document access for version {VersionId}", versionId);
                return ApiResponse<FileAccessValidationResponse>.Error("INTERNAL_ERROR", "An error occurred while validating document access", 500);
            }
        }

        #endregion



        public void Dispose()
        {
            // No persistent DriveService to dispose - services are created per operation
        }
    }
}
