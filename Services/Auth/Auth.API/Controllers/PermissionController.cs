using Auth.API.Attributes;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Permission;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Permission;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class PermissionController : ControllerBase
{
    private IPermissionService _PermissionService;
    private readonly ILogger<PermissionController> _logger;

    public PermissionController(IPermissionService PermissionService, ILogger<PermissionController> logger)
    {
        _PermissionService = PermissionService;
        _logger = logger;
    }

    [HttpGet(ApiEndPointConstant.Permission.Permissions)]
    [SkipRateLimit]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllPermissionsAsync(int page = 1, int size = 30,
        [FromQuery] PermissionFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        var response = await _PermissionService.GetAllPermissionsAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Permission.PermissionInformation + "/{permissionId}")]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEditorInformationAsync(Guid permissionId)
    {
        var response = await _PermissionService.GetPermissionInformationAsync(permissionId);
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.Permission.CreatePermission)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status201Created)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePermissionAsync([FromBody] CreatePermissionRequest request)
    {
        var response = await _PermissionService.CreatePermissionAsync(request);
        if (response == null)
        {
            _logger.LogError("Create Permission Failed");
            return Problem(MessageConstant.Permission.CreateFailed);
        }

        _logger.LogInformation("Create Permission Success");
        return Created($"{ApiEndPointConstant.Permission.PermissionInformation}/{response.Id}", response);
    }

    [HttpPatch(ApiEndPointConstant.Permission.UpdatePermission)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEditorAsync([FromBody] UpdatePermissionRequest updatePermissionRequest, Guid permissionId)
    {
        var response = await _PermissionService.UpdatePermissionAsync(updatePermissionRequest, permissionId);
        if (response == null)
        {
            _logger.LogError($"Update Permission failed");
            return Problem(MessageConstant.Permission.UpdateFailed);
        }

        _logger.LogInformation($"Update Permission successful");
        return Ok(response);
    }

    [HttpDelete(ApiEndPointConstant.Permission.DeletePermission)]
    [CustomAuthorize(Roles = new[] { Roles.Admin })]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PermissionResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeletePermissionAsync(Guid permissionId)
    {
        var response = await _PermissionService.DeletePermissionAsync(permissionId);
        if (response == null)
        {
            _logger.LogError($"Delete Permission failed");
            return Problem(MessageConstant.Permission.DeleteFailed);
        }

        _logger.LogInformation($"Delete Permission successful");
        return Ok(response);
    }
}