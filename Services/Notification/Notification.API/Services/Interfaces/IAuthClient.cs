using Notification.API.Payload.Response;

namespace Notification.API.Services.Interfaces
{
    public interface IAuthClient
    {
        // Lấy tất cả các role để cache lại ID
        Task<List<RoleResponse>> GetAllRolesAsync();

        // Lấy user theo department và role
        Task<List<UserDetailResponseExternal>> GetUsersByDepartmentAndRoleAsync(Guid departmentId, Guid roleId);

        Task<List<UserDetailResponseExternal>> GetAdminUsersAsync(Guid adminRoleId);

    }
}
