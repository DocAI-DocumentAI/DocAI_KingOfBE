using System.Reflection;
using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using Auth.API.Utils;
using Auth.Domain.Models;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement;

public class ViewerService : BaseService<ViewerService>, IViewerService
{
    private readonly IConfiguration _configuration;
    private readonly IRedisService _redisService;
    
    public ViewerService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<ViewerService> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _redisService = redisService;
        _configuration = configuration;
    }

    public async Task<ViewerResponse> GetInformationOfViewerAsync()
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var Viewer = await _unitOfWork.GetRepository<Viewer>().SingleOrDefaultAsync(
            predicate: u => u.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        if(Viewer == null)
            throw new BadHttpRequestException(MessageConstant.Viewer.ViewerNotFound);
        var response = _mapper.Map<ViewerResponse>(Viewer);
        
        return response;
    }

    public async Task<IPaginate<ViewerResponse>> GetAllViewersAsync(int page, int size, ViewerFilter? filter, string? sortBy,
        bool isAsc)
    {
        var Viewers = await _unitOfWork.GetRepository<Viewer>().GetPagingListAsync(
            selector: m => new Viewer()
            {
                Id = m.Id,
                UserId = m.UserId,
                User = m.User,
                Address = m.Address,
                CreateAt = m.CreateAt,
                UpdateAt = m.UpdateAt,
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc,
            include: m => m.Include(m => m.User));
        var responses = _mapper.Map<IPaginate<ViewerResponse>>(Viewers);
        return responses;
    }

    public async Task<ViewerResponse> UpdateViewerAsync(UpdateViewerRequest updateViewerRequest)
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var Viewer = await _unitOfWork.GetRepository<Viewer>().SingleOrDefaultAsync(
            predicate: m => m.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        Viewer.Address = string.IsNullOrEmpty(updateViewerRequest.Address) ? Viewer.Address : updateViewerRequest.Address;
        Viewer.User.Email = string.IsNullOrEmpty(updateViewerRequest.Email) ? Viewer.User.Email : updateViewerRequest.Email;
        Viewer.User.FullName = string.IsNullOrEmpty(updateViewerRequest.FullName) ? Viewer.User.FullName : updateViewerRequest.FullName;
        Viewer.User.Phone = string.IsNullOrEmpty(updateViewerRequest.Phone) ? Viewer.User.Phone : updateViewerRequest.Phone;
        Viewer.User.TwoFactorEnabled = updateViewerRequest.TwoFactorEnabled.HasValue ? updateViewerRequest.TwoFactorEnabled.Value : Viewer.User.TwoFactorEnabled;
        Viewer.User.TwoFactorMethod = string.IsNullOrEmpty(updateViewerRequest.TwoFactorMethod) ? Viewer.User.TwoFactorMethod : updateViewerRequest.TwoFactorMethod;
        Viewer.UpdateAt = DateTime.UtcNow;
        Viewer.User.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(Viewer.User);
        _unitOfWork.GetRepository<Viewer>().UpdateAsync(Viewer);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        ViewerResponse response = new ViewerResponse();
        if(isSuccess) response = _mapper.Map<ViewerResponse>(Viewer);
        return response;
    }

    public async Task<ViewerResponse> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
    {
        var userId = GetUserIdFromJwt();
        if(userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        if (string.IsNullOrEmpty(resetPasswordRequest.passwordOld))
            throw new BadHttpRequestException(MessageConstant.Viewer.PasswordOldNotNull);
        if(string.IsNullOrEmpty(resetPasswordRequest.passwordNew))
            throw new BadHttpRequestException(MessageConstant.Viewer.PasswordNewNotNull);
        if (string.IsNullOrEmpty(resetPasswordRequest.passwordConfirm))
            throw new BadHttpRequestException(MessageConstant.Viewer.PasswordConfirmNotNull);
        var Viewer = await _unitOfWork.GetRepository<Viewer>().SingleOrDefaultAsync(
            predicate: m => m.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        if(Viewer == null)
            throw new BadHttpRequestException(MessageConstant.Viewer.ViewerNotFound);
        if (!PasswordUtil.VerifyPassword(resetPasswordRequest.passwordOld, Viewer.User.Password))
            throw new BadHttpRequestException(MessageConstant.Viewer.PasswordOldWrong);
        if (resetPasswordRequest.passwordNew != resetPasswordRequest.passwordConfirm)
            throw new BadHttpRequestException(MessageConstant.Viewer.PasswordConfirmWrong);
        Viewer.User.Password = PasswordUtil.HashPassword(resetPasswordRequest.passwordNew);
        Viewer.UpdateAt = DateTime.UtcNow;
        Viewer.User.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(Viewer.User);
        _unitOfWork.GetRepository<Viewer>().UpdateAsync(Viewer);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        ViewerResponse response = null;
        if(isSuccess) response = _mapper.Map<ViewerResponse>(Viewer);
        return response;
    }
}