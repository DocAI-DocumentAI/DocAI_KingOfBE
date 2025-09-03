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
    /// Controller for folder management operations
    /// Provides hierarchical folder structure similar to Google Drive
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FolderController : ControllerBase
    {
        private readonly IFolderService _folderService;
        private readonly ILogger<FolderController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FolderController(
            IFolderService folderService,
            ILogger<FolderController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _folderService = folderService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Get folder tree structure for current user's department
        /// </summary>
        /// <param name="includeSystemFolders">Include system folders (_approved, _draft, etc.)</param>
        /// <param name="maxDepth">Maximum depth to retrieve</param>
        /// <returns>Hierarchical folder tree</returns>
        [HttpGet(ApiEndPointConstant.Folder.GetDepartmentTree)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager, Roles.Editor })]
        [ProducesResponseType(typeof(ApiResponse<FolderTreeResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDepartmentFolderTree(
            [FromQuery] bool includeSystemFolders = true,
            [FromQuery] int? maxDepth = null)
        {
            // Extract department ID from JWT token for security
            var departmentId = JwtTokenHelper.GetDepartmentId(_httpContextAccessor);
            var result = await _folderService.GetFolderTreeAsync(departmentId, includeSystemFolders, maxDepth);
            return Ok(ApiResponse<FolderTreeResponse>.Success(result, "Folder tree retrieved successfully"));
        }

        /// <summary>
        /// Get public folder tree accessible to all employees
        /// </summary>
        /// <param name="includeSystemFolders">Include system folders</param>
        /// <param name="maxDepth">Maximum depth to retrieve</param>
        /// <returns>Public folder tree</returns>
        [HttpGet(ApiEndPointConstant.Folder.GetPublicTree)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderTreeResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPublicFolderTree(
            [FromQuery] bool includeSystemFolders = true,
            [FromQuery] int? maxDepth = null)
        {
            var result = await _folderService.GetPublicFolderTreeAsync(includeSystemFolders, maxDepth);
            return Ok(ApiResponse<FolderTreeResponse>.Success(result, "Public folder tree retrieved successfully"));
        }

        /// <summary>
        /// Get folder details by ID
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>Folder details</returns>
        [HttpGet(ApiEndPointConstant.Folder.GetFolderById)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderById([FromRoute] string folderId)
        {
            var result = await _folderService.GetFolderByIdAsync(folderId);
            return Ok(ApiResponse<FolderDetailResponse>.Success(result, "Folder details retrieved successfully"));
        }

        /// <summary>
        /// Create a new folder
        /// </summary>
        /// <param name="request">Folder creation request</param>
        /// <returns>Created folder details</returns>
        [HttpPost(ApiEndPointConstant.Folder.CreateFolder)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager, Roles.Editor })]
        [ProducesResponseType(typeof(ApiResponse<FolderDetailResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            var result = await _folderService.CreateFolderAsync(request);
            return CreatedAtAction(nameof(GetFolderById), new { folderId = result.Id },
                ApiResponse<FolderDetailResponse>.Success(result, "Folder created successfully"));
        }

        /// <summary>
        /// Update folder information
        /// </summary>
        /// <param name="folderId">Folder ID to update</param>
        /// <param name="request">Update request</param>
        /// <returns>Updated folder details</returns>
        [HttpPut(ApiEndPointConstant.Folder.UpdateFolder)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager, Roles.Editor })]
        [ProducesResponseType(typeof(ApiResponse<FolderDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateFolder([FromRoute] string folderId, [FromBody] UpdateFolderRequest request)
        {
            var result = await _folderService.UpdateFolderAsync(folderId, request);
            return Ok(ApiResponse<FolderDetailResponse>.Success(result, "Folder updated successfully"));
        }

        /// <summary>
        /// Move folder to a different parent
        /// </summary>
        /// <param name="folderId">Folder ID to move</param>
        /// <param name="request">Move request with new parent</param>
        /// <returns>Updated folder details</returns>
        [HttpPost(ApiEndPointConstant.Folder.MoveFolder)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<FolderDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MoveFolder([FromRoute] string folderId, [FromBody] MoveFolderRequest request)
        {
            var result = await _folderService.MoveFolderAsync(folderId, request);
            return Ok(ApiResponse<FolderDetailResponse>.Success(result, "Folder moved successfully"));
        }

        /// <summary>
        /// Delete folder (soft delete)
        /// </summary>
        /// <param name="folderId">Folder ID to delete</param>
        /// <param name="force">Force delete even if folder contains items</param>
        /// <returns>Success status</returns>
        [HttpDelete(ApiEndPointConstant.Folder.DeleteFolder)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteFolder([FromRoute] string folderId, [FromQuery] bool force = false)
        {
            var result = await _folderService.DeleteFolderAsync(folderId, force);
            if (result)
            {
                return Ok(ApiResponse<string>.Success("Success", "Folder deleted successfully"));
            }
            else
            {
                return BadRequest(ApiResponse<object>.Error("DELETE_FAILED", "Folder could not be deleted"));
            }
        }

        /// <summary>
        /// Get folders that user has access to for document upload
        /// Use PermissionType.Edit to get folders where documents can be uploaded
        /// </summary>
        /// <param name="departmentId">User's department ID</param>
        /// <param name="permissionType">Minimum permission level required (use Edit for document upload)</param>
        /// <returns>List of accessible folders where user can upload documents</returns>
        [HttpGet(ApiEndPointConstant.Folder.GetAccessibleFolders)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<FolderSummaryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAccessibleFolders(
            [FromQuery] string? departmentId = null,
            [FromQuery] PermissionType permissionType = PermissionType.View)
        {
            var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
            var userDepartmentId = departmentId ?? JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

            var result = await _folderService.GetAccessibleFoldersAsync(userId, userDepartmentId ?? "", permissionType);
            return Ok(ApiResponse<List<FolderSummaryResponse>>.Success(result, "Accessible folders retrieved successfully"));
        }

        /// <summary>
        /// Get folder breadcrumb path
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <returns>Breadcrumb path from root to folder</returns>
        [HttpGet(ApiEndPointConstant.Folder.GetFolderBreadcrumb)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<FolderBreadcrumbResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFolderBreadcrumb([FromRoute] string folderId)
        {
            var result = await _folderService.GetFolderBreadcrumbAsync(folderId);
            return Ok(ApiResponse<List<FolderBreadcrumbResponse>>.Success(result, "Folder breadcrumb retrieved successfully"));
        }

        /// <summary>
        /// Search folders by name or path
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <param name="departmentId">Department ID (null for public folders)</param>
        /// <returns>List of matching folders</returns>
        [HttpGet(ApiEndPointConstant.Folder.SearchFolders)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<List<FolderSummaryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchFolders(
            [FromQuery] string searchTerm,
            [FromQuery] string? departmentId = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Search term is required"));
            }

            var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
            var result = await _folderService.SearchFoldersAsync(searchTerm, departmentId, userId);
            return Ok(ApiResponse<List<FolderSummaryResponse>>.Success(result, $"Found {result.Count} folders matching '{searchTerm}'"));
        }

        /// <summary>
        /// Validate folder name and path
        /// </summary>
        /// <param name="folderName">Folder name to validate</param>
        /// <param name="parentFolderId">Parent folder ID</param>
        /// <returns>Validation result</returns>
        [HttpPost(ApiEndPointConstant.Folder.ValidateFolderName)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FolderValidationResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateFolderName(
            [FromQuery] string folderName,
            [FromQuery] string? parentFolderId = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", "Folder name is required"));
            }

            var result = await _folderService.ValidateFolderNameAsync(folderName, parentFolderId);
            return Ok(ApiResponse<FolderValidationResult>.Success(result, "Folder name validation completed"));
        }
    }
}
