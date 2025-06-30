using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement;

public class RoleService : BaseService<Role>, IRoleService
{
    private readonly IConfiguration _configuration;

    public RoleService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<Role> logger, IMapper mapper,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : base(unitOfWork, logger, mapper,
        httpContextAccessor, configuration)
    {
        _configuration = configuration;
    }

    public async Task<IPaginate<RoleResponse>> GetAllRolesAsync(int page, int size, RoleFilter? filter, string? sortBy,
        bool isAsc)
    {
        var roles = await _unitOfWork.GetRepository<Role>().GetPagingListAsync(
            selector: s => new Role()
            {
                Id = s.Id,
                RoleName = s.RoleName,
                Description = s.Description,
                CreateAt = s.CreateAt,
                UpdateAt = s.UpdateAt,
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc,
            include: s => s.Include(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
        );
        var response = _mapper.Map<IPaginate<RoleResponse>>(roles);
        return response;
    }

    public async Task<RoleResponse> GetRoleInformationAsync(Guid roleId)
    {
        if (roleId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Role.RoleNotFound);
        var role = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: r => r.Id == roleId,
            include: s => s.Include(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
        );
        if (role == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);
        var response = _mapper.Map<RoleResponse>(role);
        return response;
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var role = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: s => s.RoleName == request.RoleName,
            include: s => s.Include(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
        );
        if (role != null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleExist);
        var newRole = new Role()
        {
            Id = Guid.NewGuid(),
            RoleName = request.RoleName,
            Description = request.Description,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow,
        };
        await _unitOfWork.GetRepository<Role>().InsertAsync(newRole);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        RoleResponse response = null;
        if (isSuccess) response = _mapper.Map<RoleResponse>(newRole);
        return response;
    }

    public async Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request, Guid roleId)
    {
        if (roleId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Role.RoleNotFound);
        if (request == null)
            throw new AuthenticationException(MessageConstant.Role.RoleNotNull);
        var role = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: s => s.Id == roleId,
            include: s => s.Include(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
        );
        role.RoleName = string.IsNullOrEmpty(request.RoleName) ? role.RoleName : request.RoleName;
        role.Description = string.IsNullOrEmpty(request.Description) ? role.Description : request.Description;
        role.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<Role>().UpdateAsync(role);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        RoleResponse response = null;
        if (isSuccess) response = _mapper.Map<RoleResponse>(role);
        return response;
    }

    public async Task<RoleResponse> DeleteRoleAsync(Guid roleId)
    {
        if (roleId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Role.RoleNotFound);
        var role = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: s => s.Id == roleId,
            include: s => s.Include(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
        );
        if (role == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);
        var rolePermission = await _unitOfWork.GetRepository<RolePermission>().SingleOrDefaultAsync(
            predicate: rp => rp.RoleId == roleId
            );
        if (rolePermission != null)
        {
            _unitOfWork.GetRepository<RolePermission>().DeleteAsync(rolePermission);
        }
        var RoleExist = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: r => r.RoleId == roleId,
            include: r => r.Include(r => r.Role)
            );
        if (RoleExist != null)
            throw new BadHttpRequestException(MessageConstant.Role.DeleteFailed);
        _unitOfWork.GetRepository<Role>().DeleteAsync(role);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        RoleResponse response = null;
        if (isSuccess) response = _mapper.Map<RoleResponse>(role);
        return response;
    }

    public async Task<RoleResponse> AddPermissionToRoleAsync(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role ID cannot be empty", nameof(roleId));

        if (permissionId == Guid.Empty)
            throw new ArgumentException("Permission ID cannot be empty", nameof(permissionId));

        // Kiểm tra role tồn tại
        var role = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: r => r.Id == roleId,
            include: r => r.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        );

        if (role == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Kiểm tra permission tồn tại
        var permission = await _unitOfWork.GetRepository<Permission>().SingleOrDefaultAsync(
            predicate: p => p.Id == permissionId
        );

        if (permission == null)
            throw new BadHttpRequestException(MessageConstant.Permission.PermissionNotFonnd);

        // Kiểm tra xem permission đã được gán cho role chưa
        var existingRolePermission = await _unitOfWork.GetRepository<RolePermission>().SingleOrDefaultAsync(
            predicate: rp => rp.RoleId == roleId && rp.PermissionId == permissionId
        );

        if (existingRolePermission != null)
            throw new BadHttpRequestException("Permission is already assigned to this role");

        // Tạo mối quan hệ mới giữa role và permission
        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId
        };

        // Thêm vào database
        await _unitOfWork.GetRepository<RolePermission>().InsertAsync(rolePermission);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;

        if (!isSuccess)
            throw new InvalidOperationException("Failed to add permission to role");

        // Lấy lại thông tin role đã cập nhật
        var updatedRole = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: r => r.Id == roleId,
            include: r => r.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        );

        // Trả về response
        var response = _mapper.Map<RoleResponse>(updatedRole);
        return response;
    }
}
