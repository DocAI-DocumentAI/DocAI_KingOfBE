using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request.Admin;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Admin;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using static Document.API.Attributes.AuthorizeExtensions;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for admin management of manager permissions to public folders
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class AdminPublicFolderController : ControllerBase
    {
        private readonly IAdminPublicFolderService _adminPublicFolderService;
        private readonly ILogger<AdminPublicFolderController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminPublicFolderController(
            IAdminPublicFolderService adminPublicFolderService,
            ILogger<AdminPublicFolderController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _adminPublicFolderService = adminPublicFolderService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Grant permission to a manager for a specific public folder or all public folders
        /// </summary>
        /// <param name="request">Permission grant request</param>
        /// <returns>Permission details</returns>
        [HttpPost(ApiEndPointConstant.AdminPublicFolder.GrantPermission)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<ManagerPublicFolderPermissionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GrantManagerPermission([FromBody] GrantManagerPublicFolderPermissionRequest request)
        {
            try
            {
                var result = await _adminPublicFolderService.GrantManagerPermissionAsync(request);
                return CreatedAtAction(nameof(GetManagerPublicFolderPermissions), new { managerUserId = request.ManagerUserId },
                    ApiResponse<ManagerPublicFolderPermissionResponse>.Success(result, "Manager permission granted successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("RESOURCE_NOT_FOUND", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting manager permission");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while granting permission"));
            }
        }

        /// <summary>
        /// Revoke permission from a manager for a specific public folder or all public folders
        /// </summary>
        /// <param name="request">Permission revoke request</param>
        /// <returns>Success status</returns>
        [HttpPost(ApiEndPointConstant.AdminPublicFolder.RevokePermission)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RevokeManagerPermission([FromBody] RevokeManagerPublicFolderPermissionRequest request)
        {
            try
            {
                var result = await _adminPublicFolderService.RevokeManagerPermissionAsync(request);
                return Ok(ApiResponse<bool>.Success(result, "Manager permission revoked successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("RESOURCE_NOT_FOUND", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking manager permission");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while revoking permission"));
            }
        }

        /// <summary>
        /// Grant permissions to multiple managers in a single operation
        /// </summary>
        /// <param name="request">Bulk permission grant request</param>
        /// <returns>Bulk operation results</returns>
        [HttpPost(ApiEndPointConstant.AdminPublicFolder.BulkGrantPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<BulkManagerPermissionOperationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkGrantManagerPermissions([FromBody] BulkGrantManagerPermissionsRequest request)
        {
            try
            {
                var result = await _adminPublicFolderService.BulkGrantManagerPermissionsAsync(request);
                return Ok(ApiResponse<BulkManagerPermissionOperationResponse>.Success(result, "Bulk permission operation completed"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk grant permissions operation");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred during bulk operation"));
            }
        }

        /// <summary>
        /// Get manager permissions for public folders with filtering options
        /// </summary>
        /// <param name="managerUserId">Filter by specific manager (optional)</param>
        /// <param name="publicFolderId">Filter by specific public folder (optional)</param>
        /// <param name="includeExpired">Include expired permissions</param>
        /// <param name="includeInherited">Include inherited permissions</param>
        /// <returns>List of manager permissions</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<ManagerPublicFolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetManagerPublicFolderPermissions(
            [FromQuery] string? managerUserId = null,
            [FromQuery] string? publicFolderId = null,
            [FromQuery] bool includeExpired = false,
            [FromQuery] bool includeInherited = true)
        {
            try
            {
                var request = new GetManagerPublicFolderPermissionsRequest
                {
                    ManagerUserId = managerUserId,
                    PublicFolderId = publicFolderId,
                    IncludeExpired = includeExpired,
                    IncludeInherited = includeInherited
                };

                var result = await _adminPublicFolderService.GetManagerPublicFolderPermissionsAsync(request);
                return Ok(ApiResponse<List<ManagerPublicFolderPermissionResponse>>.Success(result, "Manager permissions retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manager public folder permissions");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving permissions"));
            }
        }

        /// <summary>
        /// Get detailed access summary for a specific manager
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <returns>Manager access summary</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetManagerAccessSummary)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<ManagerPublicFolderAccessSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetManagerAccessSummary(string managerUserId)
        {
            try
            {
                var result = await _adminPublicFolderService.GetManagerAccessSummaryAsync(managerUserId);
                return Ok(ApiResponse<ManagerPublicFolderAccessSummary>.Success(result, "Manager access summary retrieved successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manager access summary for {ManagerUserId}", managerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving access summary"));
            }
        }

        /// <summary>
        /// Get manager access overview for a specific public folder
        /// </summary>
        /// <param name="publicFolderId">Public folder ID</param>
        /// <returns>Public folder manager access overview</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetFolderManagerAccess)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<PublicFolderManagerAccessOverview>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPublicFolderManagerAccessOverview(string publicFolderId)
        {
            try
            {
                var result = await _adminPublicFolderService.GetPublicFolderManagerAccessOverviewAsync(publicFolderId);
                return Ok(ApiResponse<PublicFolderManagerAccessOverview>.Success(result, "Folder manager access overview retrieved successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Error("FOLDER_NOT_FOUND", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manager access overview for folder {PublicFolderId}", publicFolderId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving access overview"));
            }
        }

        /// <summary>
        /// Get all managers who have any access to public folders
        /// </summary>
        /// <returns>List of manager access summaries</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetManagersWithAccess)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<ManagerPublicFolderAccessSummary>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllManagersWithPublicFolderAccess()
        {
            try
            {
                var result = await _adminPublicFolderService.GetAllManagersWithPublicFolderAccessAsync();
                return Ok(ApiResponse<List<ManagerPublicFolderAccessSummary>>.Success(result, "Managers with public folder access retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all managers with public folder access");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving manager access data"));
            }
        }

        /// <summary>
        /// Check if a manager has a specific permission for a public folder
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <param name="publicFolderId">Public folder ID</param>
        /// <param name="requiredPermission">Required permission type</param>
        /// <returns>Permission check result</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.CheckPermission)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckManagerPermission(
            [FromQuery] string managerUserId,
            [FromQuery] string publicFolderId,
            [FromQuery] PermissionType requiredPermission)
        {
            try
            {
                if (string.IsNullOrEmpty(managerUserId) || string.IsNullOrEmpty(publicFolderId))
                {
                    return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Manager user ID and public folder ID are required"));
                }

                var result = await _adminPublicFolderService.CheckManagerPermissionAsync(managerUserId, publicFolderId, requiredPermission);
                var responseData = new
                {
                    ManagerUserId = managerUserId,
                    PublicFolderId = publicFolderId,
                    RequiredPermission = requiredPermission.ToString(),
                    HasPermission = result
                };
                
                return Ok(ApiResponse<object>.Success(responseData, result ? "Manager has required permission" : "Manager does not have required permission"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking manager permission");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while checking permission"));
            }
        }

        /// <summary>
        /// Get public folders accessible by a manager with minimum permission level
        /// </summary>
        /// <param name="managerUserId">Manager user ID</param>
        /// <param name="minimumPermission">Minimum permission level required</param>
        /// <returns>List of accessible public folders</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetAccessibleFolders)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<ManagerPublicFolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetManagerAccessiblePublicFolders(
            string managerUserId,
            [FromQuery] PermissionType minimumPermission = PermissionType.View)
        {
            try
            {
                var result = await _adminPublicFolderService.GetManagerAccessiblePublicFoldersAsync(managerUserId, minimumPermission);
                return Ok(ApiResponse<List<ManagerPublicFolderPermissionResponse>>.Success(result, "Accessible folders retrieved successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible folders for manager {ManagerUserId}", managerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving accessible folders"));
            }
        }

        /// <summary>
        /// Get audit trail of permission changes with filtering options
        /// </summary>
        /// <param name="managerUserId">Filter by specific manager (optional)</param>
        /// <param name="publicFolderId">Filter by specific public folder (optional)</param>
        /// <param name="fromDate">Filter from date (optional)</param>
        /// <param name="toDate">Filter to date (optional)</param>
        /// <returns>Permission audit trail</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetAuditTrail)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<ManagerPublicFolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPermissionAuditTrail(
            [FromQuery] string? managerUserId = null,
            [FromQuery] string? publicFolderId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _adminPublicFolderService.GetPermissionAuditTrailAsync(managerUserId, publicFolderId, fromDate, toDate);
                return Ok(ApiResponse<List<ManagerPublicFolderPermissionResponse>>.Success(result, "Permission audit trail retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission audit trail");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving audit trail"));
            }
        }

        /// <summary>
        /// Get all public folders available for permission assignment
        /// </summary>
        /// <returns>List of all public folders</returns>
        [HttpGet(ApiEndPointConstant.AdminPublicFolder.GetAllPublicFolders)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<List<ManagerPublicFolderPermissionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllPublicFolders()
        {
            try
            {
                var result = await _adminPublicFolderService.GetAllPublicFoldersAsync();
                return Ok(ApiResponse<List<ManagerPublicFolderPermissionResponse>>.Success(result, "Public folders retrieved successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all public folders");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while retrieving public folders"));
            }
        }

        /// <summary>
        /// Cleanup expired permissions (maintenance endpoint)
        /// </summary>
        /// <returns>Number of permissions cleaned up</returns>
        [HttpPost(ApiEndPointConstant.AdminPublicFolder.CleanupExpiredPermissions)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CleanupExpiredPermissions()
        {
            try
            {
                var result = await _adminPublicFolderService.CleanupExpiredPermissionsAsync();
                var responseData = new { cleanedUpCount = result, message = $"Successfully cleaned up {result} expired permissions" };
                return Ok(ApiResponse<object>.Success(responseData, $"Cleanup completed - {result} permissions removed"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("ACCESS_DENIED", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired permissions");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred during cleanup operation"));
            }
        }
    }
}
