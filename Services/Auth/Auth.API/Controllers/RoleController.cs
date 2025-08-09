using Auth.API.Attributes;
using Auth.API.Constants;
using Auth.API.Payload.Request.Role;
using Auth.API.Payload.Response.Role;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class RoleController : ControllerBase
{
    private IRoleService _roleService;
    private readonly ILogger<RoleController> _logger;

    public RoleController(IRoleService roleService, ILogger<RoleController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách tất cả vai trò có phân trang và bộ lọc
    /// </summary>
    [HttpGet(ApiEndPointConstant.Role.Roles)]
    [SkipRateLimit]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRolesAsync(int page = 1, int size = 30,
        [FromQuery] RoleFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        var response = await _roleService.GetAllRolesAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một vai trò theo ID
    /// </summary>
    [HttpGet(ApiEndPointConstant.Role.RoleInformation + "/{roleId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEditorInformationAsync(Guid roleId)
    {
        var response = await _roleService.GetRoleInformationAsync(roleId);
        return Ok(response);
    }

    /// <summary>
    /// Tạo một vai trò mới
    /// </summary>
    [HttpPost(ApiEndPointConstant.Role.CreateRole)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRoleAsync([FromBody] CreateRoleRequest request)
    {
        var response = await _roleService.CreateRoleAsync(request);
        if (response == null)
        {
            _logger.LogError("Create Role Failed");
            return Problem(MessageConstant.Role.CreateFailed);
        }

        _logger.LogInformation("Create Role Success");
        return Created($"{ApiEndPointConstant.Role.RoleInformation}/{response.Id}", response);
    }

    /// <summary>
    /// Cập nhật thông tin một vai trò đã có theo ID
    /// </summary>
    [HttpPatch(ApiEndPointConstant.Role.UpdateRole)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEditorAsync([FromBody] UpdateRoleRequest updateRoleRequest, Guid roleId)
    {
        var response = await _roleService.UpdateRoleAsync(updateRoleRequest, roleId);
        if (response == null)
        {
            _logger.LogError($"Update role failed");
            return Problem(MessageConstant.Role.UpdateFailed);
        }

        _logger.LogInformation($"Update role successful");
        return Ok(response);
    }

    /// <summary>
    /// Xóa một vai trò theo ID
    /// </summary>
    [HttpDelete(ApiEndPointConstant.Role.DeleteRole)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRoleAsync(Guid roleId)
    {
        var response = await _roleService.DeleteRoleAsync(roleId);
        if (response == null)
        {
            _logger.LogError($"Delete failed for role {roleId}");
            return NotFound($"Role with ID {roleId} not found");
        }

        _logger.LogInformation($"Role {roleId} deleted successfully");
        return NoContent();
    }

    // [HttpPost(ApiEndPointConstant.Role.AddPermissionToRole)]
    // [CustomAuthorize(Roles = new[] { Roles.Admin })]
    // [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    // public async Task<IActionResult> AddPermissionToRoleAsync(Guid roleId, Guid permissionId)
    // {
    //     try
    //     {
    //         var response = await _roleService.AddPermissionToRoleAsync(roleId, permissionId);
    //         _logger.LogInformation($"Added permission {permissionId} to role {roleId}");
    //         return Ok(response);
    //     }
    //     catch (BadHttpRequestException ex)
    //     {
    //         _logger.LogError($"Failed to add permission to role: {ex.Message}");
    //         return BadRequest(ex.Message);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError($"Error adding permission to role: {ex.Message}");
    //         return Problem(ex.Message);
    //     }
    // }
}