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
    /// Controller for folder permission management
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FolderPermissionController : ControllerBase
    {
        private readonly IFolderService _folderService;
        private readonly ILogger<FolderPermissionController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FolderPermissionController(
            IFolderService folderService,
            ILogger<FolderPermissionController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _folderService = folderService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Get folder permissions
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>List of folder permissions</returns>
        [HttpGet(ApiEndPointConstant.FolderPermission.GetPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<List<FolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderPermissions([FromRoute] string folderId)
        {
            try
            {
                var result = await _folderService.GetFolderPermissionsAsync(folderId);
                return Ok(ApiResponse<List<FolderPermissionResponse>>.Success(result, "Folder permissions retrieved successfully"));
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
                _logger.LogError(ex, "Error retrieving permissions for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving folder permissions"));
            }
        }

        /// <summary>
        /// Set folder permission for user or department
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="request">Permission request</param>
        /// <returns>Created permission details</returns>
        [HttpPost(ApiEndPointConstant.FolderPermission.SetPermission)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<FolderPermissionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetFolderPermission(
            [FromRoute] string folderId,
            [FromBody] SetFolderPermissionRequest request)
        {
            try
            {
                var result = await _folderService.SetFolderPermissionAsync(folderId, request);
                return CreatedAtAction(nameof(GetFolderPermissions), new { folderId },
                    ApiResponse<FolderPermissionResponse>.Success(result, "Folder permission set successfully"));
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error("OPERATION_FAILED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting permission for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while setting folder permission"));
            }
        }

        /// <summary>
        /// Remove folder permission
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="permissionId">Permission ID to remove</param>
        /// <returns>Success status</returns>
        [HttpDelete(ApiEndPointConstant.FolderPermission.RemovePermission)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveFolderPermission(
            [FromRoute] string folderId,
            [FromRoute] string permissionId)
        {
            try
            {
                var result = await _folderService.RemoveFolderPermissionAsync(folderId, permissionId);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Folder permission removed successfully"));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Error("REMOVE_FAILED", "Permission could not be removed"));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", ex.Message).ToString());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("PERMISSION_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing permission {PermissionId} from folder {FolderId}", permissionId, folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while removing folder permission"));
            }
        }

        /// <summary>
        /// Check if user has permission to access folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="requiredPermission">Required permission level</param>
        /// <returns>Permission check result</returns>
        [HttpGet(ApiEndPointConstant.FolderPermission.CheckPermission)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckFolderPermission(
            [FromRoute] string folderId,
            [FromQuery] PermissionType requiredPermission = PermissionType.View)
        {
            try
            {
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var departmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var hasPermission = await _folderService.HasFolderPermissionAsync(folderId, userId, departmentId, requiredPermission);
                
                var result = new
                {
                    FolderId = folderId,
                    UserId = userId,
                    DepartmentId = departmentId,
                    RequiredPermission = requiredPermission.ToString(),
                    HasPermission = hasPermission
                };

                if (hasPermission)
                {
                    return Ok(ApiResponse<object>.Success(result, "User has required permission"));
                }
                else
                {
                    return Forbid(ApiResponse<object>.Error("ACCESS_DENIED", "User does not have required permission").ToString());
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission for folder {FolderId}", folderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while checking folder permission"));
            }
        }

        /// <summary>
        /// Initialize system folders for a department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="departmentName">Department name</param>
        /// <returns>List of created folder IDs</returns>
        [HttpPost(ApiEndPointConstant.Folder.InitializeDepartmentFolders)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitializeDepartmentFolders(
            [FromQuery] string departmentId,
            [FromQuery] string departmentName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(departmentId) || string.IsNullOrWhiteSpace(departmentName))
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Department ID and name are required"));
                }

                var result = await _folderService.InitializeDepartmentFoldersAsync(departmentId, departmentName);
                return CreatedAtAction(nameof(GetFolderPermissions), new { folderId = result.FirstOrDefault() },
                    ApiResponse<List<string>>.Success(result, "Department folders initialized successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error("OPERATION_FAILED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing folders for department {DepartmentId}", departmentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while initializing department folders"));
            }
        }

        /// <summary>
        /// Initialize public system folders
        /// </summary>
        /// <returns>List of created folder IDs</returns>
        [HttpPost(ApiEndPointConstant.Folder.InitializePublicFolders)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitializePublicFolders()
        {
            try
            {
                var result = await _folderService.InitializePublicFoldersAsync();
                return CreatedAtAction(nameof(GetFolderPermissions), new { folderId = result.FirstOrDefault() },
                    ApiResponse<List<string>>.Success(result, "Public folders initialized successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error("OPERATION_FAILED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing public folders");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while initializing public folders"));
            }
        }
    }
}
