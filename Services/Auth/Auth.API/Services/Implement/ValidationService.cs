using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;

namespace Auth.API.Services.Implement;

public class ValidationService : BaseService<ValidationService>, IValidationService
{

    public ValidationService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<ValidationService> logger,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper,
        IRedisService redisService, IGoogleOAuthService googleOAuthService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
    }

    public async Task<bool> IsDepartmentNameExistsAsync(string departmentName, Guid? excludeId = null)
    {
        var department = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Name.ToLower() == departmentName.ToLower() &&
                                                 (excludeId == null || d.Id != excludeId));
        return department != null;
    }

    public async Task<bool> IsRoleNameExistsAsync(string roleName, Guid? excludeId = null)
    {
        var role = await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.RoleName.ToLower() == roleName.ToLower() &&
                                                 (excludeId == null || r.Id != excludeId));
        return role != null;
    }

    public async Task<bool> IsPermissionNameExistsAsync(string permissionName, Guid? excludeId = null)
    {
        var permission = await _unitOfWork.GetRepository<Permission>()
            .SingleOrDefaultAsync(predicate: p => p.Name.ToLower() == permissionName.ToLower() &&
                                                 (excludeId == null || p.Id != excludeId));
        return permission != null;
    }

    public async Task<bool> IsEmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        var user = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Email.ToLower() == email.ToLower() &&
                                                 (excludeUserId == null || u.Id != excludeUserId));
        return user != null;
    }

    public async Task<bool> IsPhoneExistsAsync(string phone, Guid? excludeUserId = null)
    {
        var user = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Phone == phone &&
                                                 (excludeUserId == null || u.Id != excludeUserId));
        return user != null;
    }
}