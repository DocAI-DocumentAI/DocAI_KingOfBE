using System;
using System.Security.Authentication;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using Auth.Domain.Enums;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using Serilog;

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

    
    protected ClaimsPrincipal GetCurrentUserClaimsPrincipal() // Đổi tên và kiểu trả về
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // Bỏ logging chi tiết ở đây, di chuyển ra ngoài nếu cần để giữ hàm gọn
        // _logger.LogInformation("HttpContext: {HttpContext}", httpContext?.ToString() ?? "null");

        var user = httpContext?.User;
        if (user == null || !user.Identity.IsAuthenticated)
        {
            // _logger.LogError("User is not authenticated. Identity: {Identity}", user?.Identity?.ToString() ?? "null");
            throw new AuthenticationException("User is not authenticated.");
        }

        // Bỏ logging claims ở đây
        // var claims = user.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
        // _logger.LogInformation("Claims found: {Claims}", string.Join(", ", claims));

        // Kiểm tra cơ bản rằng có ít nhất User ID claim để đảm bảo token hợp lệ tối thiểu
        var userIdClaim = user.FindFirst("userId");
        if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
        {
            // _logger.LogError("User ID claim not found in token.");
            throw new AuthenticationException("User ID claim not found in token.");
        }

        // Không cần Guid.TryParse ở đây nữa nếu bạn chỉ trả về ClaimsPrincipal.
        // Việc parse sẽ được thực hiện ở nơi gọi nếu cần userId cụ thể.

        return user; // Trả về toàn bộ ClaimsPrincipal
    }

    protected Guid GetUserIdFromJwt()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        Log.Information("HttpContext: {HttpContext}", httpContext?.ToString() ?? "null");

        var user = httpContext?.User;
        if (user == null || !user.Identity.IsAuthenticated)
        {
            Log.Error("User is not authenticated. Identity: {Identity}", user?.Identity?.ToString() ?? "null");
            throw new AuthenticationException("User is not authenticated.");
        }

        var claims = user.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
        Log.Information("Claims found: {Claims}", string.Join(", ", claims));

        var userIdClaim = user.FindFirst("userId");
        if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
        {
            Log.Error("User ID claim not found in token.");
            throw new AuthenticationException("User ID claim not found in token.");
        }

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
        {
            Log.Error("Invalid user ID format: {Value}", userIdClaim.Value);
            throw new AuthenticationException("Invalid user ID format in token.");
        }

        return userId;
    }
}