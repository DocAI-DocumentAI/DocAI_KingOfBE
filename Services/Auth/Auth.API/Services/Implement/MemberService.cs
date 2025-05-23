using System.Reflection;
using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using Auth.API.Utils;
using Auth.Domain.Models;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MemberFilter = Auth.Infrastructure.Filter.MemberFilter;

namespace Auth.API.Services.Implement;

public class MemberService : BaseService<MemberService>, IMemberService
{
    private readonly IConfiguration _configuration;
    private readonly IRedisService _redisService;
    
    public MemberService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<MemberService> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _redisService = redisService;
        _configuration = configuration;
    }

    public async Task<MemberResponse> GetInformationOfMemberAsync()
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var member = await _unitOfWork.GetRepository<Member>().SingleOrDefaultAsync(
            predicate: u => u.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        if(member == null)
            throw new BadHttpRequestException(MessageConstant.Member.MemberNotFound);
        var response = _mapper.Map<MemberResponse>(member);
        
        return response;
    }

    public async Task<IPaginate<MemberResponse>> GetAllMembersAsync(int page, int size, MemberFilter? filter, string? sortBy,
        bool isAsc)
    {
        var members = await _unitOfWork.GetRepository<Member>().GetPagingListAsync(
            selector: m => new Member()
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
        var responses = _mapper.Map<IPaginate<MemberResponse>>(members);
        return responses;
    }

    public async Task<MemberResponse> UpdateMemberAsync(UpdateMemberRequest updateMemberRequest)
    {
        var userId = GetUserIdFromJwt();
        if (userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        var member = await _unitOfWork.GetRepository<Member>().SingleOrDefaultAsync(
            predicate: m => m.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        member.Address = string.IsNullOrEmpty(updateMemberRequest.Address) ? member.Address : updateMemberRequest.Address;
        member.User.Email = string.IsNullOrEmpty(updateMemberRequest.Email) ? member.User.Email : updateMemberRequest.Email;
        member.User.FullName = string.IsNullOrEmpty(updateMemberRequest.FullName) ? member.User.FullName : updateMemberRequest.FullName;
        member.User.Phone = string.IsNullOrEmpty(updateMemberRequest.Phone) ? member.User.Phone : updateMemberRequest.Phone;
        member.User.TwoFactorEnabled = updateMemberRequest.TwoFactorEnabled.HasValue ? updateMemberRequest.TwoFactorEnabled.Value : member.User.TwoFactorEnabled;
        member.User.TwoFactorMethod = string.IsNullOrEmpty(updateMemberRequest.TwoFactorMethod) ? member.User.TwoFactorMethod : updateMemberRequest.TwoFactorMethod;
        member.UpdateAt = DateTime.UtcNow;
        member.User.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(member.User);
        _unitOfWork.GetRepository<Member>().UpdateAsync(member);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        MemberResponse response = new MemberResponse();
        if(isSuccess) response = _mapper.Map<MemberResponse>(member);
        return response;
    }

    public async Task<MemberResponse> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
    {
        var userId = GetUserIdFromJwt();
        if(userId == null)
            throw new AuthenticationException(MessageConstant.User.UserNotFound);
        if (string.IsNullOrEmpty(resetPasswordRequest.passwordOld))
            throw new BadHttpRequestException(MessageConstant.Member.PasswordOldNotNull);
        if(string.IsNullOrEmpty(resetPasswordRequest.passwordNew))
            throw new BadHttpRequestException(MessageConstant.Member.PasswordNewNotNull);
        if (string.IsNullOrEmpty(resetPasswordRequest.passwordConfirm))
            throw new BadHttpRequestException(MessageConstant.Member.PasswordConfirmNotNull);
        var member = await _unitOfWork.GetRepository<Member>().SingleOrDefaultAsync(
            predicate: m => m.UserId == userId,
            include: m => m.Include(u => u.User)
        );
        if(member == null)
            throw new BadHttpRequestException(MessageConstant.Member.MemberNotFound);
        if (!PasswordUtil.VerifyPassword(resetPasswordRequest.passwordOld, member.User.Password))
            throw new BadHttpRequestException(MessageConstant.Member.PasswordOldWrong);
        if (resetPasswordRequest.passwordNew != resetPasswordRequest.passwordConfirm)
            throw new BadHttpRequestException(MessageConstant.Member.PasswordConfirmWrong);
        member.User.Password = PasswordUtil.HashPassword(resetPasswordRequest.passwordNew);
        member.UpdateAt = DateTime.UtcNow;
        member.User.UpdateAt = DateTime.UtcNow;
        _unitOfWork.GetRepository<User>().UpdateAsync(member.User);
        _unitOfWork.GetRepository<Member>().UpdateAsync(member);
        var isSuccess = await _unitOfWork.CommitAsync() > 0;
        MemberResponse response = null;
        if(isSuccess) response = _mapper.Map<MemberResponse>(member);
        return response;
    }
}