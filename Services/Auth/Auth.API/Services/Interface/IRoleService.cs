using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface;

public interface IRoleService
{
    public Task<IPaginate<RoleResponse>> GetAllRolesAsync(int page, int size, RoleFilter? filter, string? sortby, bool isAsc);
    public Task<RoleResponse> GetRoleInformationAsync(Guid roleId);
    public Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request);
    public Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request, Guid roleId);
    public Task<RoleResponse> DeleteRoleAsync(Guid roleId);
}