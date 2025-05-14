using System.Reflection;
using System.Security.Authentication;
using Auth.API.Constants;
using Auth.API.Models;
using Auth.API.Paginate;
using Auth.API.Payload.Response;
using Auth.API.Repository.Interfaces;
using Auth.API.Services.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MemberFilter = Auth.API.Filter.MemberFilter;

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

    public async Task<IPaginate<MemberResponse>> GetAllMemberAsync(int page, int size, MemberFilter? filter, string? sortBy,
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
}