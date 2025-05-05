using System;
using System.Security.Claims;
using Auth.API.Enums;
using Auth.API.Models;
using Auth.API.Repository.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auth.API.Services;

public class BaseService<T> where T : class
{
    protected IUnitOfWork<DocAIAuthContext> _unitOfWork;
    protected ILogger<T> _logger;
    protected IMapper _mapper;
    protected IHttpContextAccessor _httpContextAccessor;
    protected IConfiguration _configuration;
    
    public BaseService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<T> logger, IMapper mapper,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }
    protected RoleEnum GetRoleFromJwt()
    {
        string roleString = _httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(roleString)) return RoleEnum.None;
        
        Enum.TryParse<RoleEnum>(roleString, out RoleEnum role);
        return role;
        
    }

    protected Guid GetUserIdFromJwt()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("userId");
        if (userIdClaim != null)
        {
            return Guid.Parse(userIdClaim.Value);
        }
        return Guid.Empty;
    }
}