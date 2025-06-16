using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Transactions;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Staff;
using Microsoft.EntityFrameworkCore;
using Auth.API.Utils;
using Auth.Domain.Enums;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Services.Interface;

public class UserService : BaseService<UserService>, IUserService
{
    private readonly IRedisService _redisService;
    private IConfiguration _configuration;

    public UserService(IUnitOfWork<DocAIAuthContext> unitOfWork,ILogger<UserService> logger,IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper, IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
        _redisService = redisService;
    }
    
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            throw new BadHttpRequestException(MessageConstant.User.LoginRequestNoNull);
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.UserName == request.Username,
            include: u => u.Include(u => u.UserRoles).ThenInclude(u => u.Role)
                .Include(u => u.UserDepartments).ThenInclude(u => u.Department)
        );
        if (user == null || !PasswordUtil.VerifyPassword(request.Password, user.Password))
        {
            _logger.LogWarning("Login failed for username: {UserName}", request.Username);
            throw new BadHttpRequestException(MessageConstant.User.UsernameOrPasswork);
        }
        Tuple<string, Guid> guidClaim = new Tuple<string, Guid>("userId",user.Id);
        var response = new LoginResponse()
        {
            UserId = user.Id,
            Username = user.UserName,
            Email = user.Email,
            Roles = user.UserRoles?.Select(ur => new RoleResponse
            {
                RoleName = ur.Role.RoleName,
                Description = ur.Role.Description,
                CreateAt = ur.Role.CreateAt,
                UpdateAt = ur.Role.UpdateAt
            }).ToList() ?? new List<RoleResponse>(),

            Departments = user.UserDepartments?.Select(ud => new DepartmentResponse
            {
                Name = ud.Department.Name,
                Description = ud.Department.Description,
                CreateAt = ud.Department.CreateAt,
                UpdateAt = ud.Department.UpdateAt
            }).ToList() ?? new List<DepartmentResponse>(),
        };
        response.Token = JwtUtil.GenerateJwtToken(user, guidClaim, _configuration);
        response.RefreshToken = JwtUtil.GenerateRefreshToken();
        
        _unitOfWork.GetRepository<User>().UpdateAsync(user);
        bool isSuccessful = await _unitOfWork.CommitAsync() > 0;
        if (isSuccessful)
        {
            return response;
        }
        return null;
    }
    
//     public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
// {
//     if (request == null)
//         throw new ArgumentNullException(nameof(request), "Register request cannot be null");
//     
//     var usernameExists = await _unitOfWork.GetRepository<User>()
//         .SingleOrDefaultAsync(predicate:u => u.UserName == request.Username); 
//     if (usernameExists != null)
//         throw new BadHttpRequestException(MessageConstant.User.UserNameExisted);
//     
//     var phoneExists = await _unitOfWork.GetRepository<User>()
//         .SingleOrDefaultAsync(predicate:u => u.Phone == request.Phone);
//     if (phoneExists != null)
//         throw new BadHttpRequestException(MessageConstant.User.PhoneNumberExisted);
//
//
//     var key = request.Email;
//     var existingOtp = await _redisService.GetStringAsync(key); 
//     if (string.IsNullOrEmpty(existingOtp))
//         throw new BadHttpRequestException(MessageConstant.OTP.OtpNotFound);
//
//     if (existingOtp != request.Otp)
//         throw new BadHttpRequestException(MessageConstant.OTP.OtpIncorrect);
//
//     
//     await _redisService.RemoveKeyAsync(key); 
//
//     var user = _mapper.Map<User>(request);
//     user.UserId = Guid.NewGuid();
//     user.Password = PasswordUtil.HashPassword(request.Password);
//     user.Role = RoleEnum.Viewer;
//     user.CreatAt = DateTime.UtcNow; 
//     user.UpdateAt = DateTime.UtcNow; 
//
//     var member = new UserRole 
//     { 
//         Id = Guid.NewGuid(), 
//         UserId = user.UserId, 
//         User = user,
//         CreateAt = DateTime.UtcNow, 
//         UpdateAt = DateTime.UtcNow 
//     };
//
//     using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
//     {
//         try
//         {
//             await _unitOfWork.GetRepository<User>().InsertAsync(user);
//             await _unitOfWork.GetRepository<UserRole>().InsertAsync(member);
//             
//             bool isSuccessful = await _unitOfWork.CommitAsync() > 0;
//             if (!isSuccessful)
//                 throw new InvalidOperationException("Failed to save user and member data");
//
//             transaction.Complete();
//             
//             if (isSuccessful)
//             {
//                 var response = _mapper.Map<RegisterResponse>(user);
//                 response.Token = JwtUtil.GenerateJwtToken(user, new Tuple<string, Guid>("userId", user.UserId),
//                     _configuration);
//                 response.RefreshToken = JwtUtil.GenerateRefreshToken();
//                 return response;
//             }
//
//             return null;
//         }
//         catch (DbUpdateException ex)
//         {
//             _logger.LogError(ex, "Database error during user registration: {Message}. Inner: {InnerMessage}", ex.Message, ex.InnerException?.Message);
//             throw new BadHttpRequestException("Failed to register due to database error");
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Unexpected error during user registration: {Message}", ex.Message);
//             throw new BadHttpRequestException("An unexpected error occurred during registration");
//         }
//     }
// }
    
    public async Task<string> GenerateOtpAsync(GenerateEmailOtpRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Email))
            throw new BadHttpRequestException(MessageConstant.OTP.EmailRequired);

        if (_redisService == null)
            throw new InvalidOperationException(MessageConstant.Redis.RedisServiceNotInitialized);

        var key = request.Email;

        var existingOtp = await _redisService.GetStringAsync(key);
        if (!string.IsNullOrEmpty(existingOtp))
            throw new BadHttpRequestException(MessageConstant.OTP.OtpAlreadySent);

        var otp = OtpUtil.GenerateOtp();
        var subject = "Mã OTP của bạn";
        var body = $"Mã OTP của bạn là: <b>{otp}</b>. Mã này có hiệu lực trong 2 phút.";

        var response = EmailUtil.SendEmail(request.Email, subject, body, _configuration);
        _logger.LogInformation($"📧 Đã gửi email OTP: {response}");

        if (!response)
        {
            _logger.LogError($" {MessageConstant.Email.SendEmailFailed}");
            throw new BadHttpRequestException(MessageConstant.OTP.SendOtpFailed);
        }

        try
        {
            await _redisService.SetStringAsync(key, otp, TimeSpan.FromMinutes(2));
            _logger.LogInformation($" OTP [{otp}] đã được lưu vào Redis cho email {request.Email}");
            return otp;
        }
        catch (Exception ex)
        {
            _logger.LogError($" {MessageConstant.OTP.SaveOtpFailed}: {ex.Message}");
            throw new BadHttpRequestException(MessageConstant.OTP.SendOtpFailed);
        }
    }
}