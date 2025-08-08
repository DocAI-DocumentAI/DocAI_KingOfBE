using Notification.API.Payload.Response;

namespace Notification.API.Services.Interfaces
{
    public interface IUserService
    {
        Task<List<UserInfo>> GetDepartmentManagersAsync(Guid departmentId);
        Task<List<UserInfo>> GetDepartmentEditorsAsync(Guid departmentId);
        Task<List<UserInfo>> GetDocumentStakeholdersAsync(Guid documentId);
        Task<UserInfo?> GetUserByIdAsync(Guid userId);
        Task<List<UserInfo>> GetUsersByRoleAsync(string roleName);
        Task<List<UserInfo>> GetUsersByDepartmentAsync(Guid departmentId);
    }
}
