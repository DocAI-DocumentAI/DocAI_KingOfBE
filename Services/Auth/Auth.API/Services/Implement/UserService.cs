using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using Auth.API.Constants;
using Auth.API.Payload;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.User;
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
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.UserSetting;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Filter;
using System.IdentityModel.Tokens.Jwt;

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

        var user = await GetUserWithDetailsAsync(request.Email);

        if (user == null || !PasswordUtil.VerifyPassword(request.Password, user.Password))
        {
            _logger.LogWarning("Login failed for email: {Email}", request.Email);
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);
        }

        // Lấy UserSetting của user
        var userSetting = await _unitOfWork.GetRepository<UserSetting>().SingleOrDefaultAsync(
            predicate: us => us.UserId == user.Id
        );

        // Tạo response trước khi cập nhật last login
        var response = new LoginResponse
        {
            UserId = user.Id,
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
            UserSetting = userSetting != null ? new UserSettingResponse
            {
                Id = userSetting.Id,
                TwoFactorEnabled = userSetting.TwoFactorEnabled,
                TwoFactorMethod = userSetting.TwoFactorMethod,
                NotificationsEnabled = userSetting.NotificationsEnabled,
                UpdateAt = userSetting.UpdateAt
            } : null,
            Token = JwtUtil.GenerateJwtToken(user, _configuration),
            RefreshToken = JwtUtil.GenerateRefreshToken()
        };

        // Cập nhật last login sau khi đã tạo response
        await UpdateLastLoginAsync(user);

        // Publish message sau khi đã hoàn thành các thao tác với database
        _logger.LogInformation("Publishing UserRequestMessage for user: {UserId} to default exchange with timestamp {Timestamp}",
            user.Id, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        try
        {
            await _publishEndpoint.Publish(new UserRequestMessage(user.Id));
            _logger.LogInformation("✅ Successfully published message to exchange");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error publishing message: {Message}", ex.Message);
        }

        return response;
    }

    private void ValidateLoginRequest(LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
            throw new BadHttpRequestException(MessageConstant.User.LoginRequestNoNull);
    }

    private async Task<User> GetUserWithDetailsAsync(string email)
    {
        var userDetail = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Email == email,
            include: u => u.Include(u => u.Role).ThenInclude(rp => rp.RolePermissions).ThenInclude(p => p.Permission)
            .Include(u => u.Department)
            );
        return userDetail;
    }

    // CreateLoginResponse đã bị xóa vì đã tích hợp vào LoginAsync

    private async Task UpdateLastLoginAsync(User user)
    {
        var userToUpdate = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == user.Id // Chỉ cần ID của user để lấy lại
        );

        if (userToUpdate != null)
        {
            userToUpdate.UpdateAt = DateTime.UtcNow;

            _unitOfWork.GetRepository<User>().UpdateAsync(userToUpdate);
            var success = await _unitOfWork.CommitAsync() > 0;

            if (!success)
            {
                _logger.LogWarning("Login succeeded but failed to update user: {Email}", user.Email);
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
        // await ValidateOtpAsync(request.Email, request.Otp);

        var user = CreateUserEntity(request);
        var userSetting = CreateUserSettingEntity(user);
        // var activeKey = await GetActiveKeyFromActivationCodeAsync(request.ActivationCode);

        // activeKey.UsedByUserId = user.Id;
        // activeKey.Status = "Off";
        // activeKey.UpdatedAt = DateTime.UtcNow;

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                await _unitOfWork.GetRepository<User>().InsertAsync(user);
                await _unitOfWork.GetRepository<UserSetting>().InsertAsync(userSetting);

                var isSuccessful = await _unitOfWork.CommitAsync() > 0;
                if (!isSuccessful)
                    throw new InvalidOperationException("Failed to save user, role, department and permissions.");

                transaction.Complete();

                return await CreateRegisterResponse(user, userSetting);
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
        user.CreatAt = DateTime.UtcNow;
        user.UpdateAt = DateTime.UtcNow;
        return user;
    }

    private UserSetting CreateUserSettingEntity(User user)
    {
        UserSetting userSetting = null;
        userSetting.Id = Guid.NewGuid();
        userSetting.TwoFactorEnabled = false;
        userSetting.TwoFactorMethod = "Email";
        userSetting.NotificationsEnabled = true;
        userSetting.UpdateAt = DateTime.UtcNow;
        userSetting.UserId = user.Id;
        return userSetting;
    }

    // private async Task<ActiveKey> GetActiveKeyFromActivationCodeAsync(string activationCode)
    // {
    //     if (string.IsNullOrWhiteSpace(activationCode))
    //         throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);
    //
    //     var activation = await _unitOfWork.GetRepository<ActiveKey>()
    //         .SingleOrDefaultAsync(predicate: u => u.ActivationCode == activationCode);
    //
    //     if (activation == null)
    //         throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);
    //
    //     if (activation.Status == "Off")
    //         throw new BadHttpRequestException(MessageConstant.ActivationCode.ActiveKeyUsed);
    //
    //     if (activation.UsedByUserId != null)
    //         throw new BadHttpRequestException(MessageConstant.ActivationCode.ActiveKeyUsed);
    //
    //     return activation;
    // }

    private async Task<Department> GetDepartmentByIdAsync(Guid departmentId)
    {
        var deparment = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == departmentId);
        if (deparment == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        return deparment;
    }


    private async Task<RegisterResponse> CreateRegisterResponse(User user, UserSetting userSetting)
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
        response.UserSetting = new UserSettingResponse()
        {
            TwoFactorEnabled = userSetting.TwoFactorEnabled,
            TwoFactorMethod = userSetting.TwoFactorMethod,
            NotificationsEnabled = userSetting.NotificationsEnabled,
            UpdateAt = user.UpdateAt
        };
        response.Token = JwtUtil.GenerateJwtToken(user, _configuration);
        response.RefreshToken = JwtUtil.GenerateRefreshToken();

        return response;
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

    public async Task<UserRoleChangeResponse> ChangeUserRoleAsync(Guid roleId)
    {
        if (roleId == Guid.Empty)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Lấy userId từ JWT token
        var currentUserId = GetUserIdFromJwt();

        // Lấy thông tin user hiện tại
        var currentUser = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == currentUserId,
            include: u => u.Include(u => u.Role).Include(u => u.Department)
        );

        if (currentUser == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        // Lưu thông tin role cũ để trả về trong response
        var oldRole = currentUser.Role;

        // Lấy thông tin role mới
        var newRole = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
            predicate: r => r.Id == roleId
        );

        if (newRole == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Cập nhật role của user
        currentUser.RoleId = roleId;
        currentUser.UpdateAt = DateTime.UtcNow;

        // Thực hiện transaction để đảm bảo tính nhất quán
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                // Cập nhật user
                _unitOfWork.GetRepository<User>().UpdateAsync(currentUser);

                var isSuccessful = await _unitOfWork.CommitAsync() > 0;
                if (!isSuccessful)
                    throw new InvalidOperationException("Failed to update user role");

                transaction.Complete();

                // Tạo response
                var response = new UserRoleChangeResponse
                {
                    UserId = currentUser.Id,
                    OldRole = new RoleResponse
                    {
                        Id = oldRole.Id,
                        RoleName = oldRole.RoleName,
                        Description = oldRole.Description,
                        CreateAt = oldRole.CreateAt,
                        UpdateAt = oldRole.UpdateAt
                    },
                    NewRole = new RoleResponse
                    {
                        Id = newRole.Id,
                        RoleName = newRole.RoleName,
                        Description = newRole.Description,
                        CreateAt = newRole.CreateAt,
                        UpdateAt = newRole.UpdateAt
                    },
                    Department = new DepartmentResponse
                    {
                        Id = currentUser.Department.Id,
                        Name = currentUser.Department.Name,
                        Description = currentUser.Department.Description,
                        CreateAt = currentUser.Department.CreateAt,
                        UpdateAt = currentUser.Department.UpdateAt
                    },
                    ChangeDate = DateTime.UtcNow,
                    // Tạo token mới với role đã cập nhật
                    Token = JwtUtil.GenerateJwtToken(currentUser, _configuration)
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user role: {Message}", ex.Message);
                throw;
            }
        }
    }

    public async Task<ChangeDepartmentResponse> ChangeDepartmentForUserAsync(ChangeDepartmentRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Request cannot be null");

        // Lấy thông tin user hiện tại từ token
        var currentUserId = GetUserIdFromJwt();
        var currentUser = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Id == currentUserId,
                                 include: u => u.Include(u => u.Role).Include(u => u.Department));

        if (currentUser == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        // Kiểm tra quyền: phải là Admin hoặc Manager của phòng nhân sự
        bool isAdmin = currentUser.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        bool isHRManager = currentUser.Role.RoleName.Equals("Manager", StringComparison.OrdinalIgnoreCase) &&
                          currentUser.Department.Name.Equals("Phòng nhân sự", StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isHRManager)
            throw new UnauthorizedAccessException("Chỉ Admin hoặc Manager của Phòng nhân sự mới có quyền thay đổi department của user");

        // Lấy thông tin user cần thay đổi department
        var targetUser = await _unitOfWork.GetRepository<User>()
            .SingleOrDefaultAsync(predicate: u => u.Id == request.UserId,
                                 include: u => u.Include(u => u.Role).Include(u => u.Department));

        if (targetUser == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        // Lấy thông tin department mới
        var newDepartment = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == request.DepartmentId);

        if (newDepartment == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

        // Kiểm tra xem user đã ở department này chưa
        if (targetUser.DepartmentId == request.DepartmentId)
            throw new BadHttpRequestException("User đã thuộc department này rồi");

        // Lưu thông tin department cũ để trả về trong response
        var oldDepartment = targetUser.Department;

        // Cập nhật department của user
        targetUser.DepartmentId = request.DepartmentId;
        targetUser.UpdateAt = DateTime.UtcNow;

        // Thực hiện transaction để đảm bảo tính nhất quán
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                // Cập nhật user
                _unitOfWork.GetRepository<User>().UpdateAsync(targetUser);

                var isSuccessful = await _unitOfWork.CommitAsync() > 0;
                if (!isSuccessful)
                    throw new InvalidOperationException("Failed to update user department");

                transaction.Complete();

                // Lấy lại user với thông tin department mới để tạo token
                var updatedUser = await _unitOfWork.GetRepository<User>()
                    .SingleOrDefaultAsync(predicate: u => u.Id == request.UserId,
                                         include: u => u.Include(u => u.Role)
                                                      .ThenInclude(r => r.RolePermissions!)
                                                      .ThenInclude(rp => rp.Permission)
                                                      .Include(u => u.Department));

                // Tạo response
                var response = new ChangeDepartmentResponse
                {
                    UserId = targetUser.Id,
                    FullName = targetUser.FullName,
                    OldDepartment = new DepartmentResponse
                    {
                        Id = oldDepartment.Id,
                        Name = oldDepartment.Name,
                        Description = oldDepartment.Description,
                        CreateAt = oldDepartment.CreateAt,
                        UpdateAt = oldDepartment.UpdateAt
                    },
                    NewDepartment = new DepartmentResponse
                    {
                        Id = newDepartment.Id,
                        Name = newDepartment.Name,
                        Description = newDepartment.Description,
                        CreateAt = newDepartment.CreateAt,
                        UpdateAt = newDepartment.UpdateAt
                    },
                    Role = new RoleResponse
                    {
                        Id = targetUser.Role.Id,
                        RoleName = targetUser.Role.RoleName,
                        Description = targetUser.Role.Description,
                        CreateAt = targetUser.Role.CreateAt,
                        UpdateAt = targetUser.Role.UpdateAt
                    },
                    ChangeDate = DateTime.UtcNow,
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user department: {Message}", ex.Message);
                throw;
            }
        }
    }

    public async Task<List<GetUserByDeparAndRoleResponse>> GetUserByDeparAndRoleAsync(GetUserByDeparAndRole request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Request cannot be null");

        if (request.DepartmentId == Guid.Empty)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

        if (request.RoleId == Guid.Empty)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Kiểm tra department tồn tại
        var department = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == request.DepartmentId);

        if (department == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

        // Kiểm tra role tồn tại
        var role = await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.Id == request.RoleId);

        if (role == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Lấy danh sách user theo department và role với phân trang
        var users = await _unitOfWork.GetRepository<User>().GetPagingListAsync(
            selector: u => u,
            filter: null,
            predicate: u => u.DepartmentId == request.DepartmentId && u.RoleId == request.RoleId,
            include: u => u.Include(u => u.Role).Include(u => u.Department),
            page: request.PageIndex,
            size: request.PageSize,
            orderBy: u => u.OrderBy(x => x.FullName)
        );

        // Tạo danh sách response
        var responseList = users.Items.Select(user => new GetUserByDeparAndRoleResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role != null ? new RoleResponse
            {
                Id = user.Role.Id,
                RoleName = user.Role.RoleName,
                Description = user.Role.Description,
                CreateAt = user.Role.CreateAt,
                UpdateAt = user.Role.UpdateAt
            } : null,
            Department = user.Department != null ? new DepartmentResponse
            {
                Id = user.Department.Id,
                Name = user.Department.Name,
                Description = user.Department.Description,
                CreateAt = user.Department.CreateAt,
                UpdateAt = user.Department.UpdateAt
            } : null
        }).ToList();

        return responseList;
    }

    public async Task<IPaginate<UserResponse>> GetAllUsersAsync(int page, int size, UserFilter? filter, string? sortBy, bool isAsc)
    {
        var users = await _unitOfWork.GetRepository<User>().GetPagingListAsync(
            selector: s => new User()
            {
                Id = s.Id,
                Email = s.Email,
                Phone = s.Phone,
                FullName = s.FullName,
                RoleId = s.RoleId,
                Role = s.Role,
                DepartmentId = s.DepartmentId,
                Department = s.Department,
                CreatAt = s.CreatAt,
                UpdateAt = s.UpdateAt
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc,
            include: s => s.Include(u => u.Role)
                           .Include(u => u.Department)
        );

        var userIds = users.Items.Select(u => u.Id).ToList();
        var userSettings = await _unitOfWork.GetRepository<UserSetting>()
            .GetListAsync(predicate: us => userIds.Contains(us.UserId));

        var response = _mapper.Map<IPaginate<UserResponse>>(users);

        // Gán UserSetting cho từng UserResponse
        foreach (var userResponse in response.Items)
        {
            var userSetting = userSettings.FirstOrDefault(us => us.UserId == userResponse.Id);
            if (userSetting != null)
            {
                userResponse.UserSetting = new UserSettingResponse
                {
                    Id = userSetting.Id,
                    TwoFactorEnabled = userSetting.TwoFactorEnabled,
                    TwoFactorMethod = userSetting.TwoFactorMethod,
                    NotificationsEnabled = userSetting.NotificationsEnabled,
                    UpdateAt = userSetting.UpdateAt
                };
            }
        }

        return response;
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                throw new UnauthorizedAccessException("No valid token found");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);

            var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var exp = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;

            if (!string.IsNullOrEmpty(jti) && !string.IsNullOrEmpty(exp))
            {
                // Tính thời gian còn lại của token
                var expiration = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp));
                var timeRemaining = expiration - DateTimeOffset.UtcNow;

                if (timeRemaining > TimeSpan.Zero)
                {
                    await _redisService.BlacklistJwtAsync(jti, timeRemaining);
                    _logger.LogInformation("JWT token blacklisted successfully: {Jti}", jti);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw new BadHttpRequestException("Logout failed");
        }
    }
}
