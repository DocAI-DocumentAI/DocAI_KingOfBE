using Auth.API.Attributes;
using Auth.API.Constants;
using Auth.API.Payload.Request.UserPermission;
using Auth.API.Payload.Response.UserPermission;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class UserPermissionController : ControllerBase
{
    private readonly IUserPermissionService _userPermissionService;

    public UserPermissionController(IUserPermissionService userPermissionService)
    {
        _userPermissionService = userPermissionService;
    }

    /// <summary>
    /// Gán một quyền cụ thể cho người dùng.
    /// </summary>
    [HttpPost(ApiEndPointConstant.UserPermission.AddPermissionToUser)]
    public async Task<IActionResult> AddPermissionToUser([FromBody] AddUserPermissionRequest request)
    {
        var result = await _userPermissionService.AddPermissionToUserAsync(request.UserId, request.PermissionId);
        return Ok(result);
    }

    /// <summary>
    /// Xóa một quyền đã được gán cho người dùng.
    /// </summary>
    [HttpDelete(ApiEndPointConstant.UserPermission.RemovePermissionFromUser)]
    public async Task<IActionResult> RemovePermissionFromUser(Guid userId, Guid permissionId)
    {
        var result = await _userPermissionService.RemovePermissionFromUserAsync(userId, permissionId);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các quyền của một người dùng cụ thể.
    /// </summary>
    [HttpGet(ApiEndPointConstant.UserPermission.UserPermissions)]
    public async Task<IActionResult> GetUserPermissions(Guid userId)
    {
        var result = await _userPermissionService.GetUserPermissionsAsync(userId);
        return Ok(result);
    }
}