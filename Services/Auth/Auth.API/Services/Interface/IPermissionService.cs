using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Permission;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Permission;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface;

public interface IPermissionService
{
    public Task<IPaginate<PermissionResponse>> GetAllPermissionsAsync(int page, int size, PermissionFilter? filter, string? sortby, bool isAsc);
    public Task<PermissionResponse> GetPermissionInformationAsync(Guid PermissionId);
    public Task<PermissionResponse> CreatePermissionAsync(CreatePermissionRequest request);
    public Task<PermissionResponse> UpdatePermissionAsync(UpdatePermissionRequest request, Guid PermissionId);
    public Task<PermissionResponse> DeletePermissionAsync(Guid PermissionId);
}