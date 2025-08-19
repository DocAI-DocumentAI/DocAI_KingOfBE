using Document.API.Constants;
using Document.API.Models;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Domain.Enums;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using MassTransit;
using Shared.DTOs;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for synchronizing permissions between database and Google Drive
    /// </summary>
    public class PermissionSyncService : IPermissionSyncService
    {
        private readonly ILogger<PermissionSyncService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IFolderPermissionService _folderPermissionService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRequestClient<UserEmailRequest> _userEmailClient;
        private readonly IRedisService _redisService;

        public PermissionSyncService(
            ILogger<PermissionSyncService> logger,
            IUnitOfWork unitOfWork,
            IGoogleDriveService googleDriveService,
            IFolderPermissionService folderPermissionService,
            IHttpContextAccessor httpContextAccessor,
            IRequestClient<UserEmailRequest> userEmailClient,
            IRedisService redisService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _googleDriveService = googleDriveService;
            _folderPermissionService = folderPermissionService;
            _httpContextAccessor = httpContextAccessor;
            _userEmailClient = userEmailClient;
            _redisService = redisService;
        }

        public async Task<PermissionSyncResult> SyncFolderPermissionsAsync(string folderId, bool forceSync = false)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PermissionSyncResult { FolderId = folderId };

            try
            {
                _logger.LogInformation(FolderMessageConstant.GoogleDriveSync.SyncStarted, folderId);

                // Get folder from database
                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == folderId && !f.IsDeleted);

                if (folder == null)
                {
                    result.Success = false;
                    result.Message = string.Format(FolderMessageConstant.System.FolderNotFound, folderId);
                    return result;
                }

                if (string.IsNullOrEmpty(folder.GoogleDriveFolderId))
                {
                    result.Success = false;
                    result.Message = string.Format(FolderMessageConstant.SyncVerification.InvalidGoogleDriveId, folder.Name, folder.GoogleDriveFolderId ?? "null");
                    return result;
                }

                // Check if Google Drive folder exists
                var googleDriveExists = await _googleDriveService.FolderExistsAsync(folder.GoogleDriveFolderId);
                if (!googleDriveExists)
                {
                    result.Success = false;
                    result.Message = string.Format(FolderMessageConstant.SyncVerification.OrphanedFolderDetected, folder.Name, "database", "Google Drive");
                    return result;
                }

                // Get database permissions
                var dbPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.FolderId == folderId && fp.IsActive);

                // Get Google Drive permissions
                var googleDrivePermissions = await _googleDriveService.GetFilePermissionsAsync(folder.GoogleDriveFolderId);

                // Sync permissions
                var syncCount = 0;
                var failCount = 0;

                _logger.LogInformation("Found {Count} database permissions to sync for folder {FolderId}", dbPermissions.Count, folderId);

                foreach (var dbPermission in dbPermissions)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(dbPermission.UserId))
                        {
                            // Get user email from user service or JWT
                            var userEmail = await GetUserEmailAsync(dbPermission.UserId);
                            if (!string.IsNullOrEmpty(userEmail))
                            {
                                var googleRole = MapPermissionTypeToGoogleRole(dbPermission.PermissionType);
                                await _googleDriveService.GrantUserAccessAsync(folder.GoogleDriveFolderId, userEmail, folder.DepartmentId ?? "", folder.IsPublic, googleRole);
                                syncCount++;
                                _logger.LogDebug("Successfully synced {PermissionType} permission for user {UserId} ({UserEmail}) on folder {FolderId}",
                                    dbPermission.PermissionType, dbPermission.UserId, userEmail, folderId);
                            }
                            else
                            {
                                failCount++;
                                var errorMsg = $"Could not resolve email for user {dbPermission.UserId}";
                                result.Errors.Add(errorMsg);
                                _logger.LogWarning("Failed to sync permission for user {UserId} on folder {FolderId}: Could not resolve user email",
                                    dbPermission.UserId, folderId);
                            }
                        }
                        else
                        {
                            failCount++;
                            var errorMsg = $"Database permission has empty UserId";
                            result.Errors.Add(errorMsg);
                            _logger.LogWarning("Found database permission with empty UserId for folder {FolderId}", folderId);
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        result.Errors.Add($"Failed to sync permission for user {dbPermission.UserId}: {ex.Message}");
                        _logger.LogWarning(ex, "Failed to sync permission for user {UserId} on folder {FolderId}", dbPermission.UserId, folderId);
                    }
                }

                result.Success = failCount == 0;
                result.PermissionsSynced = syncCount;
                result.PermissionsFailed = failCount;
                result.Message = result.Success 
                    ? string.Format(FolderMessageConstant.GoogleDriveSync.SyncCompleted, folder.Name)
                    : string.Format(FolderMessageConstant.GoogleDriveSync.SyncFailed, folder.Name, $"{failCount} permissions failed");

                _logger.LogInformation("Permission sync completed for folder {FolderId}: {SyncCount} synced, {FailCount} failed", 
                    folderId, syncCount, failCount);

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = string.Format(FolderMessageConstant.System.UnexpectedError, ex.Message);
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "Error syncing permissions for folder {FolderId}", folderId);
                return result;
            }
            finally
            {
                result.Duration = stopwatch.Elapsed;
            }
        }

        public async Task<BulkPermissionSyncResult> SyncDepartmentPermissionsAsync(string departmentId, bool includePublic = true)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new BulkPermissionSyncResult { DepartmentId = departmentId };

            try
            {
                _logger.LogInformation(FolderMessageConstant.BulkOperations.BulkSyncStarted, "department permissions");

                // Get all folders for the department
                var folders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => !f.IsDeleted && 
                                                 (f.DepartmentId == departmentId || (includePublic && f.IsPublic)));

                result.FoldersProcessed = folders.Count;

                foreach (var folder in folders)
                {
                    var folderResult = await SyncFolderPermissionsAsync(folder.Id, false);
                    result.FolderResults.Add(folderResult);

                    if (folderResult.Success)
                    {
                        result.FoldersSuccessful++;
                        result.TotalPermissionsSynced += folderResult.PermissionsSynced;
                    }
                    else
                    {
                        result.FoldersFailed++;
                        result.Errors.AddRange(folderResult.Errors);
                    }
                }

                result.Success = result.FoldersFailed == 0;
                result.Message = string.Format(FolderMessageConstant.BulkOperations.BulkSyncCompleted, 
                    result.FoldersSuccessful, result.FoldersFailed);

                _logger.LogInformation("Bulk permission sync completed for department {DepartmentId}: {Summary}", 
                    departmentId, result.Summary);

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = string.Format(FolderMessageConstant.System.UnexpectedError, ex.Message);
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "Error in bulk permission sync for department {DepartmentId}", departmentId);
                return result;
            }
            finally
            {
                result.Duration = stopwatch.Elapsed;
            }
        }

        /// <summary>
        /// Sync permissions for all public folders
        /// </summary>
        /// <param name="forceSync">Force synchronization even if already in sync</param>
        /// <returns>Bulk permission sync result</returns>
        public async Task<BulkPermissionSyncResult> SyncPublicFoldersAsync(bool forceSync = false)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new BulkPermissionSyncResult { DepartmentId = "PUBLIC" };

            try
            {
                _logger.LogInformation("Starting sync for all public folders (forceSync: {ForceSync})", forceSync);

                // Get all public folders
                var publicFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.IsPublic && !f.IsDeleted);

                _logger.LogInformation("Found {Count} public folders to sync", publicFolders.Count);

                result.FoldersProcessed = publicFolders.Count;

                foreach (var folder in publicFolders)
                {
                    _logger.LogDebug("Syncing public folder {FolderId} ({FolderName})", folder.Id, folder.Name);

                    var folderResult = await SyncFolderPermissionsAsync(folder.Id, forceSync);
                    result.FolderResults.Add(folderResult);

                    if (folderResult.Success)
                    {
                        result.FoldersSuccessful++;
                        result.TotalPermissionsSynced += folderResult.PermissionsSynced;
                    }
                    else
                    {
                        result.FoldersFailed++;
                        result.Errors.AddRange(folderResult.Errors);
                    }
                }

                result.Success = result.FoldersFailed == 0;
                result.Message = result.Success
                    ? $"Successfully synced permissions for {result.FoldersSuccessful} public folders"
                    : $"Synced {result.FoldersSuccessful} public folders successfully, {result.FoldersFailed} failed";

                _logger.LogInformation("Public folder sync completed: {SuccessCount}/{TotalCount} successful, {TotalPermissions} permissions synced",
                    result.FoldersSuccessful, result.FoldersProcessed, result.TotalPermissionsSynced);

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error syncing public folders: {ex.Message}";
                result.Errors.Add(ex.Message);
                _logger.LogError(ex, "Error syncing public folder permissions");
                return result;
            }
            finally
            {
                result.Duration = stopwatch.Elapsed;
            }
        }

        public async Task<PermissionVerificationResult> VerifyPermissionConsistencyAsync(string folderId)
        {
            var result = new PermissionVerificationResult { FolderId = folderId };

            try
            {
                _logger.LogInformation("Verifying permission consistency for folder {FolderId}", folderId);

                // Get folder from database
                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == folderId && !f.IsDeleted);

                if (folder == null || string.IsNullOrEmpty(folder.GoogleDriveFolderId))
                {
                    result.IsConsistent = false;
                    return result;
                }

                // Get database permissions
                var dbPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.FolderId == folderId && fp.IsActive);

                result.DatabasePermissions = dbPermissions.Count;

                // Get Google Drive permissions
                var googleDrivePermissions = await _googleDriveService.GetFilePermissionsAsync(folder.GoogleDriveFolderId);
                result.GoogleDrivePermissions = googleDrivePermissions.Count;

                // Compare permissions and detect mismatches
                var mismatches = await DetectPermissionMismatchesForFolderAsync(folder, dbPermissions.ToList(), googleDrivePermissions.ToList());
                result.Mismatches = mismatches;
                result.IsConsistent = mismatches.Count == 0;

                _logger.LogInformation("Permission verification completed for folder {FolderId}: {Summary}", 
                    folderId, result.Summary);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying permission consistency for folder {FolderId}", folderId);
                result.IsConsistent = false;
                return result;
            }
        }

        public async Task<List<PermissionMismatch>> DetectPermissionMismatchesAsync(string? departmentId = null)
        {
            var mismatches = new List<PermissionMismatch>();

            try
            {
                _logger.LogInformation("Detecting permission mismatches for department: {DepartmentId}", departmentId ?? "All");

                // Get folders to check
                var folders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => !f.IsDeleted && 
                                                 !string.IsNullOrEmpty(f.GoogleDriveFolderId) &&
                                                 (departmentId == null || f.DepartmentId == departmentId || f.IsPublic));

                foreach (var folder in folders)
                {
                    try
                    {
                        var verification = await VerifyPermissionConsistencyAsync(folder.Id);
                        mismatches.AddRange(verification.Mismatches);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking folder {FolderId} for permission mismatches", folder.Id);
                    }
                }

                _logger.LogInformation("Permission mismatch detection completed: {MismatchCount} mismatches found", mismatches.Count);
                return mismatches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting permission mismatches");
                return mismatches;
            }
        }

        #region Helper Methods

        private async Task<string?> GetUserEmailAsync(string userId)
        {
            try
            {
                // Check if this is the current user from JWT
                var currentUserEmail = JwtTokenHelper.GetUserEmailOrNull(_httpContextAccessor);
                var currentUserId = JwtTokenHelper.GetUserIdOrNull(_httpContextAccessor);

                if (currentUserId == userId && !string.IsNullOrEmpty(currentUserEmail))
                {
                    // Cache the current user's email for future use
                    await _redisService.SetUserEmailAsync(userId, currentUserEmail, TimeSpan.FromHours(24));
                    return currentUserEmail;
                }

                // Try to get from cache first
                var cachedEmail = await _redisService.GetUserEmailAsync(userId);
                if (!string.IsNullOrEmpty(cachedEmail))
                {
                    _logger.LogDebug("Retrieved user email from cache for {UserId}: {Email}", userId, cachedEmail);
                    return cachedEmail;
                }

                // If not in cache, request from Auth service via RabbitMQ
                _logger.LogInformation("User email not in cache, requesting from Auth service for {UserId}", userId);

                var request = new UserEmailRequest
                {
                    UserId = userId,
                    RequestId = Guid.NewGuid().ToString()
                };

                var response = await _userEmailClient.GetResponse<UserEmailResponse>(request, timeout: TimeSpan.FromSeconds(30));

                if (response.Message.Success && !string.IsNullOrEmpty(response.Message.Email))
                {
                    var email = response.Message.Email;

                    // Cache the result for 24 hours (user emails change rarely)
                    await _redisService.SetUserEmailAsync(userId, email, TimeSpan.FromHours(24));

                    _logger.LogInformation("Retrieved email for user {UserId}: {Email}", userId, email);
                    return email;
                }
                else
                {
                    _logger.LogWarning("Failed to get user email from Auth service for user {UserId}: {ErrorMessage}", userId, response.Message.ErrorMessage);
                    return null;
                }
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting user email for user {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting email for user {UserId}", userId);
                return null;
            }
        }

        private static string MapPermissionTypeToGoogleRole(PermissionType permissionType)
        {
            return permissionType switch
            {
                PermissionType.View => "reader",
                PermissionType.Edit => "writer",
                PermissionType.Manage => "writer", // Google Drive doesn't have admin role for files
                _ => "reader"
            };
        }

        private async Task<List<PermissionMismatch>> DetectPermissionMismatchesForFolderAsync(
            Folder folder, 
            List<FolderPermission> dbPermissions, 
            List<Google.Apis.Drive.v3.Data.Permission> googlePermissions)
        {
            var mismatches = new List<PermissionMismatch>();

            // TODO: Implement detailed permission comparison logic
            // This is a placeholder for the actual implementation

            return mismatches;
        }

        #endregion

        // Placeholder implementations for remaining interface methods
        public Task<PermissionRepairResult> RepairPermissionMismatchesAsync(List<PermissionMismatch> mismatches, bool dryRun = true)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<PermissionSyncResult> GrantPermissionAsync(string folderId, string userEmail, string permissionType, string departmentId)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<PermissionSyncResult> RevokePermissionAsync(string folderId, string userEmail)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<UserPermissionSyncResult> SyncUserPermissionsAsync(string userEmail, string departmentId)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<FolderPermissionStatus> GetFolderPermissionStatusAsync(string folderId)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<PermissionSyncHealthResult> PerformHealthCheckAsync()
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }

        public async Task<PermissionSyncStatistics> GetSyncStatisticsAsync(string? departmentId = null)
        {
            throw new NotImplementedException("To be implemented in next iteration");
        }
    }
}
