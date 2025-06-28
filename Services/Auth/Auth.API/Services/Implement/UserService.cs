using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using Auth.API.Constants;
using Auth.API.Payload;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.ActiveKey;
using Auth.API.Payload.Response.Staff;
using Microsoft.EntityFrameworkCore;
using Auth.API.Utils;
using Auth.Domain.Enums;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using MassTransit;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Shared.DTOs;

namespace Auth.API.Services.Interface;

public class UserService : BaseService<UserService>, IUserService
{
    private readonly IRedisService _redisService;
    private IConfiguration _configuration;
    private IPublishEndpoint _publishEndpoint;

    public UserService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<UserService> logger,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper,
        IRedisService redisService, IPublishEndpoint publishEndpoint) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
        _redisService = redisService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        ValidateLoginRequest(request);

        var user = await GetUserWithDetailsAsync(request.Username);

        if (user == null || !PasswordUtil.VerifyPassword(request.Password, user.Password))
        {
            _logger.LogWarning("Login failed for username: {UserName}", request.Username);
            throw new BadHttpRequestException(MessageConstant.User.UsernameOrPasswork);
        }

        var response = CreateLoginResponse(user);
        await UpdateLastLoginAsync(user);

        await _publishEndpoint.Publish(new UserRequestMessage(user.Id));

        return await response;
    }

    private void ValidateLoginRequest(LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
            throw new BadHttpRequestException(MessageConstant.User.LoginRequestNoNull);
    }

    private async Task<User> GetUserWithDetailsAsync(string username)
    {
        var userDetail = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.UserName == username,
            include: u => u.Include(u => u.Role).ThenInclude(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
            .Include(u => u.Department)
            );
        return userDetail;
    }

    private async Task<LoginResponse> CreateLoginResponse(User user)
    {
        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = new RoleResponse
            {
                Id = user.Role.Id,
                RoleName = user.Role.RoleName,
                Description = user.Role.Description,
                CreateAt = user.Role.CreateAt,
                UpdateAt = user.Role.UpdateAt
            },
            Department = new DepartmentResponse
            {
                Id = user.Department.Id,
                Name = user.Department.Name,
                Description = user.Department.Description,
                CreateAt = user.Department.CreateAt,
                UpdateAt = user.Department.UpdateAt
            },
            Token = JwtUtil.GenerateJwtToken(user, _configuration),
            RefreshToken = JwtUtil.GenerateRefreshToken()
        };
    }

    private async Task UpdateLastLoginAsync(User user)
    {
        // Bước 1: Lấy lại user từ DB bằng một truy vấn MỚI.
        // Điều này đảm bảo rằng đối tượng 'userToUpdate' này được theo dõi bởi DbContext hiện tại
        // và sẽ không có xung đột với các đối tượng 'NoTracking' từ GetUserWithDetailsAsync.
        var userToUpdate = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == user.Id // Chỉ cần ID của user để lấy lại
        );

        // Bước 2: Kiểm tra nếu user được tìm thấy để cập nhật
        if (userToUpdate != null)
        {
            // Bước 3: Thực hiện thay đổi trên đối tượng đã được theo dõi
            userToUpdate.UpdateAt = DateTime.UtcNow; // Cập nhật thời gian cuối cùng đăng nhập
            // userToUpdate.LastLogin = DateTime.UtcNow; // Nếu bạn có trường LastLogin riêng biệt

            // Bước 4: Gọi UpdateAsync trên đối tượng đã được theo dõi
            // Phương thức UpdateAsync của bạn sẽ hoạt động đúng ở đây vì userToUpdate đã được theo dõi
            _unitOfWork.GetRepository<User>().UpdateAsync(userToUpdate);
            var success = await _unitOfWork.CommitAsync() > 0;

            if (!success)
            {
                _logger.LogWarning("Login succeeded but failed to update user: {UserName}", user.UserName);
            }
        }
        else
        {
            _logger.LogError("User not found for update after successful login: {UserId}", user.Id);
        }
    }


    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Register request cannot be null");

        await ValidateUniqueFieldsAsync(request);
        await ValidateOtpAsync(request.Email, request.Otp);

        var user = CreateUserEntity(request);
        var activeKey = await GetActiveKeyFromActivationCodeAsync(request.ActivationCode);
        user.RoleId = activeKey.RoleId;
        user.DepartmentId = activeKey.DepartmentId;

        activeKey.UsedByUserId = user.Id;
        activeKey.Status = "Off";
        activeKey.UpdatedAt = DateTime.UtcNow;

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                await _unitOfWork.GetRepository<User>().InsertAsync(user);
                _unitOfWork.GetRepository<ActiveKey>().UpdateAsync(activeKey);
                if (!string.IsNullOrWhiteSpace(request.ActivationCode))
                {
                    var activationCodeExists = await _unitOfWork.GetRepository<ActiveKey>()
                        .SingleOrDefaultAsync(predicate: u => u.ActivationCode == request.ActivationCode);
                    if (activationCodeExists != null)
                    {
                        _unitOfWork.GetRepository<ActiveKey>().DeleteAsync(activationCodeExists);
                    }
                }

                var isSuccessful = await _unitOfWork.CommitAsync() > 0;
                if (!isSuccessful)
                    throw new InvalidOperationException("Failed to save user, role, department and permissions.");

                transaction.Complete();

                return await CreateRegisterResponse(user);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error during user registration: {Message}. Inner: {InnerMessage}",
                    ex.Message, ex.InnerException?.Message);
                throw new BadHttpRequestException("Failed to register due to database error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user registration: {Message}", ex.Message);
                throw new BadHttpRequestException("An unexpected error occurred during registration");
            }
        }
    }


    private async Task ValidateUniqueFieldsAsync(RegisterRequest request)
    {
        var repo = _unitOfWork.GetRepository<User>();

        if (await repo.SingleOrDefaultAsync(predicate: u => u.UserName == request.Username) != null)
            throw new BadHttpRequestException(MessageConstant.User.UserNameExisted);

        if (await repo.SingleOrDefaultAsync(predicate: u => u.Phone == request.Phone) != null)
            throw new BadHttpRequestException(MessageConstant.User.PhoneNumberExisted);

        if (await repo.SingleOrDefaultAsync(predicate: u => u.Email == request.Email) != null)
            throw new BadHttpRequestException(MessageConstant.User.EmailExisted);
    }

    private async Task ValidateOtpAsync(string email, string otp)
    {
        var existingOtp = await _redisService.GetStringAsync(email);
        if (string.IsNullOrEmpty(existingOtp))
            throw new BadHttpRequestException(MessageConstant.OTP.OtpNotFound);

        if (existingOtp != otp)
            throw new BadHttpRequestException(MessageConstant.OTP.OtpIncorrect);

        await _redisService.RemoveKeyAsync(email);
    }

    private User CreateUserEntity(RegisterRequest request)
    {
        var user = _mapper.Map<User>(request);
        user.Id = Guid.NewGuid();
        user.Password = PasswordUtil.HashPassword(request.Password);
        user.TwoFactorEnabled = false;
        user.TwoFactorMethod = "Email";
        user.CreatAt = DateTime.UtcNow;
        user.UpdateAt = DateTime.UtcNow;
        return user;
    }

    private async Task<ActiveKey> GetActiveKeyFromActivationCodeAsync(string activationCode)
    {
        if (string.IsNullOrWhiteSpace(activationCode))
            throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

        var activation = await _unitOfWork.GetRepository<ActiveKey>()
            .SingleOrDefaultAsync(predicate: u => u.ActivationCode == activationCode);

        if (activation == null)
            throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

        if (activation.Status == "Off")
            throw new BadHttpRequestException(MessageConstant.ActivationCode.ActiveKeyUsed);

        if (activation.UsedByUserId != null)
            throw new BadHttpRequestException(MessageConstant.ActivationCode.ActiveKeyUsed);

        return activation;
    }

    private async Task<Department> GetDepartmentByIdAsync(Guid departmentId)
    {
        var deparment = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == departmentId);
        if (deparment == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        return deparment;
    }


    private async Task<RegisterResponse> CreateRegisterResponse(User user)
    {
        var response = _mapper.Map<RegisterResponse>(user);
        response.Department = new DepartmentResponse
        {
            Name = user.Department.Name,
            Description = user.Department.Description,
            CreateAt = user.Department.CreateAt,
            UpdateAt = user.Department.UpdateAt
        };
        response.Role = new RoleResponse
        {
            RoleName = user.Role.RoleName,
            Description = user.Role.Description,
            CreateAt = user.Role.CreateAt,
            UpdateAt = user.Role.UpdateAt
        };
        response.Token = JwtUtil.GenerateJwtToken(user, _configuration);
        response.RefreshToken = JwtUtil.GenerateRefreshToken();

        return response;
    }

    public async Task<ActiveKeyResponse> CreateActiveKeyAsync(ActiveKeyRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Request cannot be null");

        // Lấy thông tin user hiện tại từ token
        var currentUserId = GetUserIdFromJwt();
        var currentUser = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Id == currentUserId,
                                 include: u => u.Include(u => u.Role).Include(u => u.Department));

        // Lấy thông tin role mục tiêu
        var targetRole = await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.Id == request.RoleId);

        if (targetRole == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Lấy thông tin department mục tiêu
        var targetDepartment = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == request.DepartmentId);

        if (targetDepartment == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

        // Kiểm tra quyền dựa trên role hierarchy
        int currentUserRoleLevel = ParseRole(currentUser.Role.RoleName);
        int targetRoleLevel = ParseRole(targetRole.RoleName);

        if (currentUserRoleLevel <= targetRoleLevel)
            throw new UnauthorizedAccessException($"You cannot create an activation code for role '{targetRole.RoleName}' because your role level is not high enough");

        // Kiểm tra quyền dựa trên department
        bool isAdmin = currentUser.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        // Nếu không phải admin, chỉ được tạo ActiveKey cho department của mình
        if (!isAdmin && currentUser.DepartmentId != request.DepartmentId)
            throw new UnauthorizedAccessException("You can only create activation codes for your own department");

        // Tạo activation code
        var code = await GenerateActivationCode();

        // Tạo ActiveKey
        var activeKey = new ActiveKey
        {
            Id = Guid.NewGuid(),
            ActivationCode = code,
            Status = "On",
            UsedByUserId = null,
            CreatedByUserId = currentUserId,
            RoleId = targetRole.Id,
            DepartmentId = targetDepartment.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Lưu vào database
        await _unitOfWork.GetRepository<ActiveKey>().InsertAsync(activeKey);
        var isSuccessful = await _unitOfWork.CommitAsync() > 0;

        if (!isSuccessful)
            throw new InvalidOperationException("Failed to save activation code");

        // Trả về response
        return new ActiveKeyResponse
        {
            Id = activeKey.Id,
            ActivationCode = activeKey.ActivationCode,
            Status = activeKey.Status,
            Role = targetRole,
            Department = targetDepartment,
            CreatedAt = activeKey.CreatedAt,
            UpdatedAt = activeKey.UpdatedAt
        };
    }

    public async Task<string> GenerateActivationCode(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var code = new StringBuilder(length);
        using (var rng = RandomNumberGenerator.Create())
        {
            var bytes = new byte[sizeof(uint)];
            while (code.Length < length)
            {
                rng.GetBytes(bytes);
                uint random = BitConverter.ToUInt32(bytes, 0);
                code.Append(chars[(int)(random % (uint)chars.Length)]);
            }
        }
        return code.ToString();
    }

    private int ParseRole(string roleName)
    {
        return roleName.ToLowerInvariant() switch
        {
            "admin" => 3,
            "manager" => 2,
            "editor" => 1,
            "member" => 0,
            _ => 0,
        };
    }


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
