using Document.API.Constants;
using Document.API.Models;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for folder synchronization operations between database and Google Drive
    /// </summary>
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [Authorize]
    public class FolderSyncController : ControllerBase
    {
        private readonly ILogger<FolderSyncController> _logger;
        private readonly IFolderService _folderService;
        private readonly IPermissionSyncService _permissionSyncService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FolderSyncController(
            ILogger<FolderSyncController> logger,
            IFolderService folderService,
            IPermissionSyncService permissionSyncService,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _folderService = folderService;
            _permissionSyncService = permissionSyncService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Verify folder synchronization between database and Google Drive
        /// </summary>
        /// <param name="departmentId">Optional department ID to filter verification</param>
        /// <returns>Sync verification result</returns>
        [HttpGet(ApiEndPointConstant.FolderSync.VerifySync)]
        public async Task<ActionResult<FolderSyncVerificationResult>> VerifyFolderSyncAsync([FromQuery] string? departmentId = null)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // If departmentId is not specified, use user's department
                var targetDepartmentId = departmentId ?? userDepartmentId;

                _logger.LogInformation("User {UserId} requested folder sync verification for department {DepartmentId}", 
                    userId, targetDepartmentId);

                var result = await _folderService.VerifyFolderSyncAsync(targetDepartmentId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying folder sync");
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Verify permission synchronization for a specific folder
        /// </summary>
        /// <param name="folderId">Folder ID to verify permissions</param>
        /// <returns>Permission verification result</returns>
        [HttpGet(ApiEndPointConstant.FolderSync.VerifyPermissions)]
        public async Task<ActionResult<PermissionVerificationResult>> VerifyFolderPermissionsAsync([Required] string folderId)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                _logger.LogInformation("User {UserId} requested permission verification for folder {FolderId}", userId, folderId);

                var result = await _permissionSyncService.VerifyPermissionConsistencyAsync(folderId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying folder permissions for {FolderId}", folderId);
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Synchronize permissions for a specific folder
        /// </summary>
        /// <param name="folderId">Folder ID to synchronize permissions</param>
        /// <param name="forceSync">Force synchronization even if already in sync</param>
        /// <returns>Permission sync result</returns>
        [HttpPost(ApiEndPointConstant.FolderSync.SyncPermissions)]
        public async Task<ActionResult<PermissionSyncResult>> SyncFolderPermissionsAsync(
            [Required] string folderId, 
            [FromQuery] bool forceSync = false)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                _logger.LogInformation("User {UserId} requested permission sync for folder {FolderId} (force: {ForceSync})", 
                    userId, folderId, forceSync);

                var result = await _permissionSyncService.SyncFolderPermissionsAsync(folderId, forceSync);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing folder permissions for {FolderId}", folderId);
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Synchronize permissions for all folders in a department
        /// </summary>
        /// <param name="departmentId">Department ID to synchronize</param>
        /// <param name="includePublic">Include public folders in synchronization</param>
        /// <returns>Bulk permission sync result</returns>
        [HttpPost("sync-department-permissions/{departmentId}")]
        public async Task<ActionResult<BulkPermissionSyncResult>> SyncDepartmentPermissionsAsync(
            [Required] string departmentId,
            [FromQuery] bool includePublic = true)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                _logger.LogInformation("User {UserId} requested department permission sync for {DepartmentId} (includePublic: {IncludePublic})", 
                    userId, departmentId, includePublic);

                var result = await _permissionSyncService.SyncDepartmentPermissionsAsync(departmentId, includePublic);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing department permissions for {DepartmentId}", departmentId);
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Detect permission mismatches across folders
        /// </summary>
        /// <param name="departmentId">Optional department ID to filter detection</param>
        /// <returns>List of permission mismatches</returns>
        [HttpGet("detect-permission-mismatches")]
        public async Task<ActionResult<List<PermissionMismatch>>> DetectPermissionMismatchesAsync([FromQuery] string? departmentId = null)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // If departmentId is not specified, use user's department
                var targetDepartmentId = departmentId ?? userDepartmentId;

                _logger.LogInformation("User {UserId} requested permission mismatch detection for department {DepartmentId}", 
                    userId, targetDepartmentId);

                var result = await _permissionSyncService.DetectPermissionMismatchesAsync(targetDepartmentId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting permission mismatches");
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Get synchronization statistics
        /// </summary>
        /// <param name="departmentId">Optional department ID to filter statistics</param>
        /// <returns>Sync statistics</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<PermissionSyncStatistics>> GetSyncStatisticsAsync([FromQuery] string? departmentId = null)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // If departmentId is not specified, use user's department
                var targetDepartmentId = departmentId ?? userDepartmentId;

                _logger.LogInformation("User {UserId} requested sync statistics for department {DepartmentId}", 
                    userId, targetDepartmentId);

                var result = await _permissionSyncService.GetSyncStatisticsAsync(targetDepartmentId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync statistics");
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Perform health check on synchronization system
        /// </summary>
        /// <returns>Health check result</returns>
        [HttpGet("health")]
        public async Task<ActionResult<PermissionSyncHealthResult>> PerformHealthCheckAsync()
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                _logger.LogInformation("User {UserId} requested sync health check", userId);

                var result = await _permissionSyncService.PerformHealthCheckAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing sync health check");
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed permission status for a folder
        /// </summary>
        /// <param name="folderId">Folder ID to get permission status</param>
        /// <returns>Detailed permission status</returns>
        [HttpGet("permission-status/{folderId}")]
        public async Task<ActionResult<FolderPermissionStatus>> GetFolderPermissionStatusAsync([Required] string folderId)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                _logger.LogInformation("User {UserId} requested permission status for folder {FolderId}", userId, folderId);

                var result = await _permissionSyncService.GetFolderPermissionStatusAsync(folderId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder permission status for {FolderId}", folderId);
                return StatusCode(500, new { message = FolderMessageConstant.System.UnexpectedError, details = ex.Message });
            }
        }
    }
}
