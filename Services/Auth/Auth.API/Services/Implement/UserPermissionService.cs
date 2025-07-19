using Auth.API.Payload.Request.UserPermission;
using Auth.API.Payload.Response.UserPermission;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement;

public class UserPermissionService : BaseService<UserPermissionService>, IUserPermissionService
{
    private IConfiguration _configuration;

    public UserPermissionService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<UserPermissionService> logger,
         IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
    }

    public async Task<UserPermissionResponse> AddPermissionToUserAsync(Guid userId, Guid permissionId)
    {
        // Kiểm tra user tồn tại
        var user = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Id == userId);
        if (user == null)
            throw new BadHttpRequestException("User not found");

        // Kiểm tra permission tồn tại
        var permission = await _unitOfWork.GetRepository<Permission>()
            .SingleOrDefaultAsync(predicate: p => p.Id == permissionId);
        if (permission == null)
            throw new BadHttpRequestException("Permission not found");

        // Kiểm tra đã có permission chưa
        var existingUserPermission = await _unitOfWork.GetRepository<UserPermission>()
            .SingleOrDefaultAsync(predicate: up => up.UserId == userId && up.PermissionId == permissionId);
        if (existingUserPermission != null)
            throw new BadHttpRequestException("User already has this permission");

        // Tạo UserPermission mới
        var userPermission = new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PermissionId = permissionId
        };

        await _unitOfWork.GetRepository<UserPermission>().InsertAsync(userPermission);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;

        if (!isSuccess)
            throw new InvalidOperationException("Failed to add permission to user");

        return new UserPermissionResponse
        {
            Id = userPermission.Id,
            UserId = userId,
            PermissionId = permissionId,
            PermissionName = permission.Name
        };
    }

    public async Task<bool> RemovePermissionFromUserAsync(Guid userId, Guid permissionId)
    {
        var userPermission = await _unitOfWork.GetRepository<UserPermission>()
            .SingleOrDefaultAsync(predicate: up => up.UserId == userId && up.PermissionId == permissionId);

        if (userPermission == null)
            return false;

        _unitOfWork.GetRepository<UserPermission>().DeleteAsync(userPermission);
        return await _unitOfWork.CommitAsync() > 0;
    }

    public async Task<List<UserPermissionResponse>> GetUserPermissionsAsync(Guid userId)
    {
        var userPermissions = await _unitOfWork.GetRepository<UserPermission>()
            .GetListAsync(
                predicate: up => up.UserId == userId,
                include: up => up.Include(x => x.Permission)
            );

        return userPermissions.Select(up => new UserPermissionResponse
        {
            Id = up.Id,
            UserId = up.UserId,
            PermissionId = up.PermissionId,
            PermissionName = up.Permission.Name
        }).ToList();
    }
}