using System.Reflection;
using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Permission;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Permission;
using Auth.API.Services.Interface;
using Auth.API.Utils;
using Auth.Domain.Models;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement;

public class PermissionService : BaseService<PermissionService>, IPermissionService
{
    private readonly IConfiguration _configuration;
    private readonly IRedisService _redisService;

    public PermissionService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<PermissionService> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _redisService = redisService;
        _configuration = configuration;
    }

    public async Task<IPaginate<PermissionResponse>> GetAllPermissionsAsync(int page, int size, PermissionFilter? filter, string? sortBy,
        bool isAsc)
    {
        var permissions = await _unitOfWork.GetRepository<Permission>().GetPagingListAsync(
            selector: s => new Permission()
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                CreateAt = s.CreateAt,
                UpdateAt = s.UpdateAt,
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc
        );
        var response = _mapper.Map<IPaginate<PermissionResponse>>(permissions);
        return response;
    }

    public async Task<PermissionResponse> GetPermissionInformationAsync(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Permission.PermissionNotFonnd);
        var permission = await _unitOfWork.GetRepository<Permission>().SingleOrDefaultAsync(
            predicate: r => r.Id == permissionId
        );
        if (permission == null)
            throw new BadHttpRequestException(MessageConstant.Permission.PermissionNotFonnd);
        var response = _mapper.Map<PermissionResponse>(permission);
        return response;
    }

    public async Task<PermissionResponse> CreatePermissionAsync(CreatePermissionRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var permission = await _unitOfWork.GetRepository<Permission>().SingleOrDefaultAsync(
            predicate: s => s.Name == request.PermissonName
        );
        if (permission != null)
            throw new BadHttpRequestException(MessageConstant.Permission.PermissionExist);
        var newPermission = new Permission()
        {
            Id = Guid.NewGuid(),
            Name = request.PermissonName,
            Description = request.Description,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow,
        };
        await _unitOfWork.GetRepository<Permission>().InsertAsync(newPermission);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        PermissionResponse response = null;
        if (isSuccess) response = _mapper.Map<PermissionResponse>(newPermission);
        return response;
    }

    public async Task<PermissionResponse> UpdatePermissionAsync(UpdatePermissionRequest request, Guid permissionId)
    {
        if (permissionId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Permission.PermissionNotFonnd);
        if (request == null)
            throw new AuthenticationException(MessageConstant.Permission.PermissionNotNull);
        var permission = await _unitOfWork.GetRepository<Permission>().SingleOrDefaultAsync(
            predicate: s => s.Id == permissionId
        );
        permission.Name = string.IsNullOrEmpty(request.PermissionName) ? permission.Name : request.PermissionName;
        permission.Description = string.IsNullOrEmpty(request.Description) ? permission.Description : request.Description;
        permission.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<Permission>().UpdateAsync(permission);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        PermissionResponse response = null;
        if (isSuccess) response = _mapper.Map<PermissionResponse>(permission);
        return response;
    }

    public async Task<PermissionResponse> DeletePermissionAsync(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Permission.PermissionNotFonnd);
        var permission = await _unitOfWork.GetRepository<Permission>().SingleOrDefaultAsync(
            predicate: s => s.Id == permissionId
        );
        if (permission == null)
            throw new BadHttpRequestException(MessageConstant.Permission.PermissionNotFonnd);
        var userPermission = await _unitOfWork.GetRepository<UserPermission>().SingleOrDefaultAsync(
            predicate: ur => ur.PermissionId == permissionId
        );
        if (userPermission != null)
        {
            _unitOfWork.GetRepository<UserPermission>().DeleteAsync(userPermission);
        }
        _unitOfWork.GetRepository<Permission>().DeleteAsync(permission);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        PermissionResponse response = null;
        if (isSuccess) response = _mapper.Map<PermissionResponse>(permission);
        return response;
    }
}