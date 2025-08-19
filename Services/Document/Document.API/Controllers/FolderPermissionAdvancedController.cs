using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request.Folder;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Folder;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using static Document.API.Attributes.AuthorizeExtensions;

namespace Document.API.Controllers
{
    /// <summary>
    /// Advanced controller for folder permission management
    /// Provides detailed permission analysis, bulk operations, and conflict resolution
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FolderPermissionAdvancedController : ControllerBase
    {
        private readonly IFolderPermissionService _folderPermissionService;
        private readonly ILogger<FolderPermissionAdvancedController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FolderPermissionAdvancedController(
            IFolderPermissionService folderPermissionService,
            ILogger<FolderPermissionAdvancedController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _folderPermissionService = folderPermissionService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Get detailed permission breakdown for a user on a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="userId">User ID (optional - defaults to current user)</param>
        /// <returns>Detailed permission breakdown</returns>
        [HttpGet(ApiEndPointConstant.FolderPermissionAdvanced.GetPermissionBreakdown)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<FolderPermissionBreakdownResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPermissionBreakdown(
            [FromRoute] string folderId,
            [FromQuery] string? userId = null)
        {
            try
            {
                var targetUserId = userId ?? JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var result = await _folderPermissionService.GetPermissionBreakdownAsync(folderId, targetUserId, userDepartmentId);
                return Ok(ApiResponse<FolderPermissionBreakdownResponse>.Success(result, "Permission breakdown retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission breakdown for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting permission breakdown"));
            }
        }

        /// <summary>
        /// Bulk set permissions for multiple users/departments on a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="requests">List of permission requests</param>
        /// <param name="applyToSubfolders">Whether to apply to all subfolders</param>
        /// <returns>List of created/updated permissions</returns>
        [HttpPost(ApiEndPointConstant.FolderPermissionAdvanced.BulkSetPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<List<FolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkSetPermissions(
            [FromRoute] string folderId,
            [FromBody] List<SetFolderPermissionRequest> requests,
            [FromQuery] bool applyToSubfolders = false)
        {
            try
            {
                if (!requests.Any())
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "At least one permission request is required"));
                }

                var result = await _folderPermissionService.BulkSetPermissionsAsync(folderId, requests, applyToSubfolders);
                return Ok(ApiResponse<List<FolderPermissionResponse>>.Success(result, $"Successfully set {result.Count} permissions"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk setting permissions on folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while setting permissions"));
            }
        }

        /// <summary>
        /// Inherit permissions from parent folder
        /// </summary>
        /// <param name="folderId">Child folder ID</param>
        /// <param name="parentFolderId">Parent folder ID</param>
        /// <param name="overrideExisting">Whether to override existing permissions</param>
        /// <returns>Number of permissions inherited</returns>
        [HttpPost(ApiEndPointConstant.FolderPermissionAdvanced.InheritPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InheritPermissions(
            [FromRoute] string folderId,
            [FromQuery] string parentFolderId,
            [FromQuery] bool overrideExisting = false)
        {
            try
            {
                if (string.IsNullOrEmpty(parentFolderId))
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Parent folder ID is required"));
                }

                var inheritedCount = await _folderPermissionService.InheritPermissionsFromParentAsync(folderId, parentFolderId, overrideExisting);
                
                var result = new { InheritedCount = inheritedCount, ParentFolderId = parentFolderId, OverrideExisting = overrideExisting };
                return Ok(ApiResponse<object>.Success(result, $"Successfully inherited {inheritedCount} permissions from parent folder"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inheriting permissions for folder {FolderId} from parent {ParentId}", folderId, parentFolderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while inheriting permissions"));
            }
        }

        /// <summary>
        /// Propagate permissions to all subfolders
        /// </summary>
        /// <param name="folderId">Parent folder ID</param>
        /// <param name="permissionType">Permission type to propagate</param>
        /// <param name="targetUserId">Target user ID (optional)</param>
        /// <param name="targetDepartmentId">Target department ID (optional)</param>
        /// <returns>Number of subfolders affected</returns>
        [HttpPost(ApiEndPointConstant.FolderPermissionAdvanced.PropagatePermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PropagatePermissions(
            [FromRoute] string folderId,
            [FromQuery] PermissionType permissionType,
            [FromQuery] string? targetUserId = null,
            [FromQuery] string? targetDepartmentId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(targetUserId) && string.IsNullOrEmpty(targetDepartmentId))
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Either target user ID or department ID must be provided"));
                }

                var affectedCount = await _folderPermissionService.PropagatePermissionsToSubfoldersAsync(folderId, permissionType, targetUserId, targetDepartmentId);
                
                var result = new 
                { 
                    AffectedSubfolders = affectedCount, 
                    PermissionType = permissionType.ToString(),
                    TargetUserId = targetUserId,
                    TargetDepartmentId = targetDepartmentId
                };

                return Ok(ApiResponse<object>.Success(result, $"Successfully propagated {permissionType} permission to {affectedCount} subfolders"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error propagating permissions for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while propagating permissions"));
            }
        }

        /// <summary>
        /// Get all folders a user has access to with specific permission level
        /// </summary>
        /// <param name="requiredPermission">Required permission level</param>
        /// <param name="departmentId">Filter by department (optional)</param>
        /// <param name="userId">Target user ID (optional - defaults to current user)</param>
        /// <returns>List of accessible folders</returns>
        [HttpGet(ApiEndPointConstant.FolderPermissionAdvanced.GetUserAccessibleFolders)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<FolderAccessResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserAccessibleFolders(
            [FromQuery] PermissionType requiredPermission = PermissionType.View,
            [FromQuery] string? departmentId = null,
            [FromQuery] string? userId = null)
        {
            try
            {
                var targetUserId = userId ?? JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var result = await _folderPermissionService.GetUserAccessibleFoldersAsync(targetUserId, userDepartmentId, requiredPermission, departmentId);
                return Ok(ApiResponse<List<FolderAccessResponse>>.Success(result, $"Found {result.Count} accessible folders"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible folders for user");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting accessible folders"));
            }
        }

        /// <summary>
        /// Validate if a user can perform a specific action on a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="action">Action to validate</param>
        /// <param name="userId">Target user ID (optional - defaults to current user)</param>
        /// <returns>Validation result with details</returns>
        [HttpGet(ApiEndPointConstant.FolderPermissionAdvanced.ValidateAction)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<PermissionValidationResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateAction(
            [FromRoute] string folderId,
            [FromQuery] FolderAction action,
            [FromQuery] string? userId = null)
        {
            try
            {
                var targetUserId = userId ?? JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var result = await _folderPermissionService.ValidateActionAsync(folderId, targetUserId, userDepartmentId, action);
                return Ok(ApiResponse<PermissionValidationResult>.Success(result, $"Action validation completed: {(result.IsAllowed ? "Allowed" : "Denied")}"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating action {Action} for folder {FolderId}", action, folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while validating action"));
            }
        }
    }
}
