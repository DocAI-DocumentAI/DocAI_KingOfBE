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

    [HttpGet(ApiEndPointConstant.Role.Roles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllRolesAsync(int page = 1, int size = 30,
        [FromQuery] RoleFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        var response = await _roleService.GetAllRolesAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Role.RoleInformation + "/{roleId}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEditorInformationAsync(Guid roleId)
    {
        var response = await _roleService.GetRoleInformationAsync(roleId);
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.Role.CreateRole)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
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

    [HttpPatch(ApiEndPointConstant.Role.UpdateRole)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
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

    [HttpDelete(ApiEndPointConstant.Role.DeleteRole)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRoleAsync(Guid roleId)
    {
        var response = await _roleService.DeleteRoleAsync(roleId);
        if (response == null)
        {
            _logger.LogError($"Delete role failed");
            return Problem(MessageConstant.Role.DeleteFailed);
        }

        _logger.LogInformation($"Delete role successful");
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.Role.AddPermissionToRole)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddPermissionToRoleAsync(Guid roleId, Guid permissionId)
    {
        try
        {
            var response = await _roleService.AddPermissionToRoleAsync(roleId, permissionId);
            _logger.LogInformation($"Added permission {permissionId} to role {roleId}");
            return Ok(response);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to add permission to role: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding permission to role: {ex.Message}");
            return Problem(ex.Message);
        }
    }
}
