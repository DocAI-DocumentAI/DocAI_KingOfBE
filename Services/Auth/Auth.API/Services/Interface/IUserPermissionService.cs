using Auth.API.Payload.Request.UserPermission;
using Auth.API.Payload.Response.UserPermission;

namespace Auth.API.Services.Interface;

public interface IUserPermissionService
{
    Task<UserPermissionResponse> AddPermissionToUserAsync(Guid userId, Guid permissionId);
    Task<bool> RemovePermissionFromUserAsync(Guid userId, Guid permissionId);
    Task<List<UserPermissionResponse>> GetUserPermissionsAsync(Guid userId);
}