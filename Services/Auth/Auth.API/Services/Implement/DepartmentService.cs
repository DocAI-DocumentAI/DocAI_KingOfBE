using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Payload.Request.Department;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;

namespace Auth.API.Services.Implement;

public class DepartmentService : BaseService<DepartmentService>, IDepartmentService
{
    private readonly IConfiguration _configuration;
    private readonly IRedisService _redisService;

    public DepartmentService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<DepartmentService> logger,
        IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration,
        IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _redisService = redisService;
        _configuration = configuration;
    }

    public async Task<IPaginate<DepartmentResponse>> GetAllDepartmentsAsync(int page, int size,
        DepartmentFilter? filter, string? sortBy,
        bool isAsc)
    {
        var departments = await _unitOfWork.GetRepository<Department>().GetPagingListAsync(
            selector: s => new Department()
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
        var response = _mapper.Map<IPaginate<DepartmentResponse>>(departments);
        return response;
    }

    public async Task<DepartmentResponse> GetDepartmentInformationAsync(Guid departmentId)
    {
        if (departmentId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Department.DepartmentNotFound);
        var department = await _unitOfWork.GetRepository<Department>().SingleOrDefaultAsync(
            predicate: r => r.Id == departmentId
        );
        if (department == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        var response = _mapper.Map<DepartmentResponse>(department);
        return response;
    }

    public async Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var department = await _unitOfWork.GetRepository<Department>().SingleOrDefaultAsync(
            predicate: s => s.Name == request.DepartmentName
        );
        if (department != null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentExist);
        var newDepartment = new Department()
        {
            Id = Guid.NewGuid(),
            Name = request.DepartmentName,
            Description = request.Description,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow,
        };
        await _unitOfWork.GetRepository<Department>().InsertAsync(newDepartment);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        DepartmentResponse response = null;
        if (isSuccess) response = _mapper.Map<DepartmentResponse>(newDepartment);
        return response;
    }

    public async Task<DepartmentResponse> UpdateDepartmentAsync(UpdateDepartmentRequest request, Guid departmentId)
    {
        if (departmentId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Department.DepartmentNotFound);
        if (request == null)
            throw new AuthenticationException(MessageConstant.Department.DepartmentNotNull);
        var department = await _unitOfWork.GetRepository<Department>().SingleOrDefaultAsync(
            predicate: s => s.Id == departmentId
        );
        if (department == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        department.Name = string.IsNullOrEmpty(request.DepartmentName) ? department.Name : request.DepartmentName;
        department.Description =
            string.IsNullOrEmpty(request.Description) ? department.Description : request.Description;
        department.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<Department>().UpdateAsync(department);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        DepartmentResponse response = null;
        if (isSuccess) response = _mapper.Map<DepartmentResponse>(department);
        return response;
    }

    public async Task<DepartmentResponse> DeleteDepartmentAsync(Guid departmentId)
    {
        if (departmentId == Guid.Empty)
            throw new AuthenticationException(MessageConstant.Department.DepartmentNotFound);
        var department = await _unitOfWork.GetRepository<Department>().SingleOrDefaultAsync(
            predicate: s => s.Id == departmentId
        );
        if (department == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        var userDepartment = await _unitOfWork.GetRepository<UserDepartment>().SingleOrDefaultAsync(
            predicate: ur => ur.DepartmentId == departmentId
        );
        var departmentRolePermission =
            await _unitOfWork.GetRepository<DepartmentRolePermission>().SingleOrDefaultAsync(
                predicate: drp => drp.DepartmentId == departmentId
            );

        if (departmentRolePermission != null)
        {
            _unitOfWork.GetRepository<DepartmentRolePermission>().DeleteAsync(departmentRolePermission);
        }
        if (userDepartment != null)
        {
            _unitOfWork.GetRepository<UserDepartment>().DeleteAsync(userDepartment);
        }
        _unitOfWork.GetRepository<Department>().DeleteAsync(department);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        DepartmentResponse response = null;
        if (isSuccess) response = _mapper.Map<DepartmentResponse>(department);
        return response;
    }
}