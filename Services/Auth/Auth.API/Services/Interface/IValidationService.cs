namespace Auth.API.Services.Interface;

public interface IValidationService
{
    Task<bool> IsDepartmentNameExistsAsync(string departmentName, Guid? excludeId = null);
    Task<bool> IsRoleNameExistsAsync(string roleName, Guid? excludeId = null);
    Task<bool> IsPermissionNameExistsAsync(string permissionName, Guid? excludeId = null);
    Task<bool> IsEmailExistsAsync(string email, Guid? excludeUserId = null);
    Task<bool> IsPhoneExistsAsync(string phone, Guid? excludeUserId = null);
}