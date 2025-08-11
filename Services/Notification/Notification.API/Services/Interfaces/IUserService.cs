using Notification.API.Payload.Response;
using Shared.DTOs;

namespace Notification.API.Services.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetDepartmentManagersAsync(Guid departmentId);
        Task<List<UserDto>> GetDepartmentEditorsAsync(Guid departmentId);
        Task<List<UserDto>> GetDocumentStakeholdersAsync(string documentId);
        Task<UserDto?> GetUserByIdAsync(Guid userId);
        Task<List<UserDto>> GetUsersByRoleAsync(string roleName);
        Task<List<UserDto>> GetUsersByDepartmentAsync(Guid departmentId);
    }
}
