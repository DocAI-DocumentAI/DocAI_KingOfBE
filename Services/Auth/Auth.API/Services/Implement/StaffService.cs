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

public class StaffService : BaseService<Staff> , IStaffService
{
    private readonly IConfiguration _configuration;
    public StaffService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<Staff> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
    }

    public async Task<IPaginate<StaffResponse>> GetAllStaffsAsync(int page, int size, StaffFilter? filter, string? sortBy, bool isAsc)
    {
        var staffs = await _unitOfWork.GetRepository<Staff>().GetPagingListAsync(
            selector: s => new Staff()
            {
                Id = s.Id,
                UserId = s.UserId,
                User = s.User,
                Type = s.Type,
                CreateAt = s.CreateAt,
                UpdateAt = s.UpdateAt,
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc,
            include: s => s.Include(s => s.User)
            );
        var response = _mapper.Map<IPaginate<StaffResponse>>(staffs);
        return response;
    }

    public async Task<StaffResponse> GetStaffInformationAsync()
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var staff = await _unitOfWork.GetRepository<Staff>().SingleOrDefaultAsync(
            predicate: s => s.UserId == userId,
            include: s => s.Include(s => s.User)
            );
        if (staff == null)
            throw new BadHttpRequestException(MessageConstant.Staff.StaffNotFound);
        var response = _mapper.Map<StaffResponse>(staff);
        return response;
    }

    public async Task<StaffResponse> UpdateStaffAsync(UpdateStaffRequest request)
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var staff = await _unitOfWork.GetRepository<Staff>().SingleOrDefaultAsync(
            predicate: s => s.UserId == userId,
            include: s => s.Include(s => s.User)
            );
        staff.User.Email = string.IsNullOrEmpty(request.Email) ? staff.User.Email : request.Email;
        staff.User.FullName = string.IsNullOrEmpty(request.FullName) ? staff.User.FullName : request.FullName;
        staff.User.Phone = string.IsNullOrEmpty(request.Phone) ? staff.User.Phone : request.Phone;
        staff.User.TwoFactorEnabled = request.TwoFactorEnabled.HasValue ? request.TwoFactorEnabled.Value : staff.User.TwoFactorEnabled;
        staff.User.TwoFactorMethod = string.IsNullOrEmpty(request.TwoFactorMethod) ? staff.User.TwoFactorMethod : request.TwoFactorMethod;
        staff.Type = string.IsNullOrEmpty(request.Type) ? staff.Type : request.Type;
        staff.UpdateAt = DateTime.UtcNow;
        staff.User.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(staff.User);
        _unitOfWork.GetRepository<Staff>().UpdateAsync(staff);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        StaffResponse response = null;
        if (isSuccess) response = _mapper.Map<StaffResponse>(staff);
        return response;
    }
}