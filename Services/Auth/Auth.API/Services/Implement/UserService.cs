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
using Shared.Commands;
using Auth.API.Payload.Response.Role;
using Auth.API.Payload.Response.Department;
using Auth.API.Payload.Response.UserSetting;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Filter;
using System.IdentityModel.Tokens.Jwt;
using Auth.API.Payload.Response.Permission;
using Auth.API.Payload.Request.Auth;
using Auth.API.Payload.Request.GG;
using Auth.API.Payload.Response.Auth;

namespace Auth.API.Services.Interface;

public class UserService : BaseService<UserService>, IUserService
{
    private readonly IRedisService _redisService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private IConfiguration _configuration;
    private IPublishEndpoint _publishEndpoint;

    public UserService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<UserService> logger,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper,
        IRedisService redisService, IPublishEndpoint publishEndpoint, IGoogleOAuthService googleOAuthService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
        _redisService = redisService;
        _publishEndpoint = publishEndpoint;
        _googleOAuthService = googleOAuthService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        ValidateLoginRequest(request);

        var user = await GetUserWithDetailsAsync(request.Email);

        if (user == null || !PasswordUtil.VerifyPassword(request.Password, user.Password) || user.Active == false)
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
            RequirePasswordChange = user.RequirePasswordChange,
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
            Permissions = user.UserPermissions?.Select(up => new PermissionResponse
            {
                Id = up.Permission.Id,
                Name = up.Permission.Name,
                Description = up.Permission.Description,
                CreateAt = up.Permission.CreateAt,
                UpdateAt = up.Permission.UpdateAt
            }).ToList() ?? new List<PermissionResponse>(),
        };

        // Generate tokens
        response.DocaiToken = JwtUtil.GenerateJwtToken(user, _configuration);
        response.DocaiRefreshToken = JwtUtil.GenerateJwtRefreshToken(user, _configuration);

        // Check if user has Google tokens in Redis
        var googleTokens = await _redisService.GetGoogleTokensAsync(user.Id.ToString());
        if (googleTokens.HasValue)
        {
            response.GoogleAccessToken = googleTokens.Value.accessToken;
            response.GoogleRefreshToken = googleTokens.Value.refreshToken;
        }

        // Store DocAI tokens in Redis
        await _redisService.SetDocAITokensAsync(user.Id.ToString(),
            response.DocaiToken, response.DocaiRefreshToken);

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
        _logger.LogInformation("Searching for user with email: {Email}", email);

        var userDetail = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Email.ToLower() == email.ToLower(), // Case insensitive
            include: u => u.Include(u => u.Role)
            .Include(u => u.Department)
            .Include(u => u.UserPermissions)
            .ThenInclude(up => up.Permission)
            );

        if (userDetail == null)
        {
            _logger.LogWarning("No user found with email: {Email}", email);
        }
        else
        {
            _logger.LogInformation("Found user: {UserId} with email: {Email}", userDetail.Id, userDetail.Email);
        }

        return userDetail;
    }

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


    public async Task<RegisterResponse> CreateUserAsync(RegisterRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request), "Register request cannot be null");

        await ValidateUniqueFieldsAsync(request);

        var user = CreateUserEntity(request);
        var userSetting = CreateUserSettingEntity(user);

        try
        {
            await _unitOfWork.GetRepository<User>().InsertAsync(user);
            await _unitOfWork.GetRepository<UserSetting>().InsertAsync(userSetting);

            // Handle permissions
            await AssignPermissionsToUserAsync(user.Id, request.PermissionIds);

            var isSuccessful = await _unitOfWork.CommitAsync() > 0;
            if (!isSuccessful)
                throw new InvalidOperationException("Failed to save user and user settings.");

            var response = await CreateRegisterResponse(user, userSetting);

            // Setup Google Drive permissions for the new user
            try
            {
                var command = new SetupUserGoogleDrivePermissionsCommand
                {
                    UserId = user.Id.ToString(),
                    UserEmail = user.Email,
                    DepartmentId = user.DepartmentId.ToString(),
                    DepartmentName = $"Department {user.DepartmentId}" // We'll get the actual name from the department service
                };

                await _publishEndpoint.Publish(command);
                _logger.LogInformation("Published Google Drive permission setup command for user {UserEmail}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Google Drive permission setup command for user {UserId}. User created successfully but permissions setup may be delayed.", user.Id);
                // Don't fail the user creation if Google Drive setup publishing fails
            }

            return response;
        }
        catch
        {
            // Rollback logic if needed
            throw;
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
        user.Active = true;
        user.Password = PasswordUtil.HashPassword(request.Password);
        user.CreatAt = DateTime.UtcNow;
        user.UpdateAt = DateTime.UtcNow;
        return user;
    }

    private UserSetting CreateUserSettingEntity(User user)
    {
        var userSetting = new UserSetting();
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
        var userWithDetails = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == user.Id,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission)
        );

        var response = _mapper.Map<RegisterResponse>(userWithDetails ?? user);
        response.UserId = userWithDetails?.Id ?? Guid.Empty;
        response.Department = new DepartmentResponse
        {
            Id = userWithDetails?.Department?.Id ?? Guid.Empty,
            Name = userWithDetails?.Department?.Name ?? "Unknown",
            Description = userWithDetails?.Department?.Description ?? "",
            CreateAt = userWithDetails?.Department?.CreateAt ?? DateTime.UtcNow,
            UpdateAt = userWithDetails?.Department?.UpdateAt ?? DateTime.UtcNow
        };

        response.UserSetting = new UserSettingResponse
        {
            Id = userSetting.Id,
            TwoFactorEnabled = userSetting.TwoFactorEnabled,
            TwoFactorMethod = userSetting.TwoFactorMethod,
            NotificationsEnabled = userSetting.NotificationsEnabled,
            UpdateAt = userSetting.UpdateAt
        };

        // Map permissions
        response.Permissions = userWithDetails?.UserPermissions?
            .Select(up => new PermissionResponse
            {
                Id = up.Permission.Id,
                Name = up.Permission.Name,
                Description = up.Permission.Description,
                CreateAt = up.Permission.CreateAt,
                UpdateAt = up.Permission.UpdateAt
            }).ToList() ?? new List<PermissionResponse>();

        response.DocaiToken = JwtUtil.GenerateJwtToken(userWithDetails ?? user, _configuration);
        response.DocaiRefreshToken = JwtUtil.GenerateJwtRefreshToken(userWithDetails ?? user, _configuration);

        // Check if user has Google tokens in Redis
        var googleTokens = await _redisService.GetGoogleTokensAsync(user.Id.ToString());
        if (googleTokens.HasValue)
        {
            response.GoogleAccessToken = googleTokens.Value.accessToken;
            response.GoogleRefreshToken = googleTokens.Value.refreshToken;
        }

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
        var subject = "Mã OTP Xác Thực - Bảo Mật Tài Khoản";

        // Custom HTML email template
        var body = GenerateOtpEmailTemplate(otp);

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

    private string GenerateOtpEmailTemplate(string otp)
    {
        return $@"
            <!DOCTYPE html>
            <html lang='vi'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Mã OTP Xác Thực</title>
                <style>
                    body {{
                        margin: 0;
                        padding: 0;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        background-color: #f5f5f5;
                        line-height: 1.6;
                    }}
                    .container {{
                        max-width: 600px;
                        margin: 0 auto;
                        background-color: #ffffff;
                        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                    }}
                    .header {{
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: white;
                        padding: 30px;
                        text-align: center;
                    }}
                    .header h1 {{
                        margin: 0;
                        font-size: 28px;
                        font-weight: 600;
                    }}
                    .content {{
                        padding: 40px 30px;
                        text-align: center;
                    }}
                    .otp-box {{
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: white;
                        padding: 20px;
                        border-radius: 12px;
                        margin: 30px 0;
                        display: inline-block;
                        box-shadow: 0 4px 15px rgba(102, 126, 234, 0.3);
                    }}
                    .otp-code {{
                        font-size: 36px;
                        font-weight: bold;
                        letter-spacing: 8px;
                        margin: 0;
                        text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
                    }}
                    .message {{
                        font-size: 16px;
                        color: #333;
                        margin: 20px 0;
                    }}
                    .warning {{
                        background-color: #fff3cd;
                        border: 1px solid #ffeaa7;
                        color: #856404;
                        padding: 15px;
                        border-radius: 8px;
                        margin: 20px 0;
                        font-size: 14px;
                    }}
                    .footer {{
                        background-color: #f8f9fa;
                        padding: 20px;
                        text-align: center;
                        border-top: 1px solid #e9ecef;
                    }}
                    .footer p {{
                        margin: 0;
                        font-size: 14px;
                        color: #6c757d;
                    }}
                    .icon {{
                        font-size: 48px;
                        margin-bottom: 20px;
                    }}
                    .btn {{
                        display: inline-block;
                        padding: 12px 30px;
                        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                        color: white;
                        text-decoration: none;
                        border-radius: 25px;
                        font-weight: 600;
                        margin-top: 20px;
                        transition: transform 0.2s;
                    }}
                    .btn:hover {{
                        transform: translateY(-2px);
                    }}
                    @media (max-width: 600px) {{
                        .container {{
                            margin: 0 10px;
                        }}
                        .header, .content {{
                            padding: 20px;
                        }}
                        .otp-code {{
                            font-size: 28px;
                            letter-spacing: 4px;
                        }}
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <div class='icon'>🔐</div>
                        <h1>Mã OTP Xác Thực</h1>
                    </div>
                    
                    <div class='content'>
                        <p class='message'>Chào bạn,</p>
                        <p class='message'>Bạn đã yêu cầu mã OTP để xác thực tài khoản. Vui lòng sử dụng mã dưới đây:</p>
                        
                        <div class='otp-box'>
                            <p class='otp-code'>{otp}</p>
                        </div>
                        
                        <div class='warning'>
                            <strong>⚠️ Lưu ý quan trọng:</strong><br>
                            • Mã OTP này có hiệu lực trong <strong>2 phút</strong><br>
                            • Không chia sẻ mã này với bất kỳ ai<br>
                            • Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email
                        </div>
                        
                        <p class='message'>Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi!</p>
                    </div>
                    
                    <div class='footer'>
                        <p>© 2025 Công ty DocAI. Mọi quyền được bảo lưu.</p>
                        <p>Email này được gửi tự động, vui lòng không reply.</p>
                    </div>
                </div>
            </body>
            </html>";
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
                                                      .Include(u => u.Department)
                                                      .Include(u => u.UserPermissions)
                                                      .ThenInclude(up => up.Permission)
                                        );

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

    // public async Task<List<GetUserByDeparAndRoleResponse>> GetUserByDeparAndRoleAsync(GetUserByDeparAndRole request)
    // {
    //     if (request == null)
    //         throw new ArgumentNullException(nameof(request), "Request cannot be null");

    //     if (request.DepartmentId == Guid.Empty)
    //         throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

    //     if (request.RoleId == Guid.Empty)
    //         throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

    //     // Kiểm tra department tồn tại
    //     var department = await _unitOfWork.GetRepository<Department>()
    //         .SingleOrDefaultAsync(predicate: d => d.Id == request.DepartmentId);

    //     if (department == null)
    //         throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

    //     // Kiểm tra role tồn tại
    //     var role = await _unitOfWork.GetRepository<Role>()
    //         .SingleOrDefaultAsync(predicate: r => r.Id == request.RoleId);

    //     if (role == null)
    //         throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

    //     // Lấy danh sách user theo department và role với phân trang
    //     var users = await _unitOfWork.GetRepository<User>().GetPagingListAsync(
    //         selector: u => u,
    //         filter: null,
    //         predicate: u => u.DepartmentId == request.DepartmentId && u.RoleId == request.RoleId,
    //         include: u => u.Include(u => u.Role).Include(u => u.Department),
    //         page: request.PageIndex,
    //         size: request.PageSize,
    //         orderBy: u => u.OrderBy(x => x.FullName)
    //     );

    //     // Tạo danh sách response
    //     var responseList = users.Items.Select(user => new GetUserByDeparAndRoleResponse
    //     {
    //         UserId = user.Id,
    //         FullName = user.FullName,
    //         Email = user.Email,
    //         Phone = user.Phone,
    //         Role = user.Role != null ? new RoleResponse
    //         {
    //             Id = user.Role.Id,
    //             RoleName = user.Role.RoleName,
    //             Description = user.Role.Description,
    //             CreateAt = user.Role.CreateAt,
    //             UpdateAt = user.Role.UpdateAt
    //         } : null,
    //         Department = user.Department != null ? new DepartmentResponse
    //         {
    //             Id = user.Department.Id,
    //             Name = user.Department.Name,
    //             Description = user.Department.Description,
    //             CreateAt = user.Department.CreateAt,
    //             UpdateAt = user.Department.UpdateAt
    //         } : null
    //     }).ToList();

    //     return responseList;
    // }

    public async Task<IPaginate<UserResponse>> GetAllUsersAsync(int page, int size, UserFilter? filter, string? sortBy, bool isAsc)
    {
        var users = await _unitOfWork.GetRepository<User>().GetPagingListAsync(
            selector: s => new User()
            {
                Id = s.Id,
                Email = s.Email,
                Phone = s.Phone,
                Active = s.Active,
                FullName = s.FullName,
                RoleId = s.RoleId,
                Role = s.Role,
                DepartmentId = s.DepartmentId,
                Department = s.Department,
                CreatAt = s.CreatAt,
                UpdateAt = s.UpdateAt,
                UserPermissions = s.UserPermissions
            },
            page: page,
            size: size,
            filter: filter,
            sortBy: sortBy,
            isAsc: isAsc,
            include: s => s.Include(u => u.Role)
                           .Include(u => u.Department)
                           .Include(u => u.UserPermissions)
                           .ThenInclude(up => up.Permission)
        );

        var userIds = users.Items.Select(u => u.Id).ToList();
        var userSettings = await _unitOfWork.GetRepository<UserSetting>()
            .GetListAsync(predicate: us => userIds.Contains(us.UserId));

        var response = _mapper.Map<IPaginate<UserResponse>>(users);

        // Gán UserSetting và Permissions cho từng UserResponse
        foreach (var userResponse in response.Items)
        {
            var user = users.Items.FirstOrDefault(u => u.Id == userResponse.Id);

            // Map UserSetting
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

            // Map Permissions
            userResponse.Permissions = user?.UserPermissions?.Select(up => new PermissionResponse
            {
                Id = up.Permission.Id,
                Name = up.Permission.Name,
                Description = up.Permission.Description,
                CreateAt = up.Permission.CreateAt,
                UpdateAt = up.Permission.UpdateAt
            }).ToList() ?? new List<PermissionResponse>();
        }

        return response;
    }

    public async Task<bool> LogoutAsync()
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return false;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var jti = JwtUtil.ExtractJtiFromToken(token);
        var expiration = JwtUtil.GetTokenExpiration(token);

        if (!string.IsNullOrEmpty(jti) && expiration.HasValue)
        {
            var timeRemaining = expiration.Value - DateTime.UtcNow;
            if (timeRemaining > TimeSpan.Zero)
            {
                await _redisService.BlacklistJwtAsync(jti, timeRemaining);
                _logger.LogInformation("JWT token blacklisted successfully: {Jti}", jti);
            }
        }

        return true;
    }

    public async Task<Dictionary<string, string>> GetUserNamesByIdsAsync(List<string> userIds)
    {
        try
        {
            var result = new Dictionary<string, string>();

            if (!userIds.Any())
                return result;

            // Convert string IDs to Guids and filter valid ones
            var validGuids = new List<Guid>();
            foreach (var userId in userIds)
            {
                if (Guid.TryParse(userId, out var guid))
                {
                    validGuids.Add(guid);
                }
            }

            if (!validGuids.Any())
                return result;

            // Bulk query users from database
            var users = await _unitOfWork.GetRepository<User>().GetListAsync(
                predicate: u => validGuids.Contains(u.Id),
                selector: u => new { u.Id, u.FullName }
            );

            // Map results back to string IDs
            foreach (var user in users)
            {
                result[user.Id.ToString()] = string.IsNullOrEmpty(user.FullName) ? "Unknown User" : user.FullName;
            }

            _logger.LogInformation("Retrieved names for {Count} users", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user names by IDs");
            return new Dictionary<string, string>();
        }
    }

    public async Task<GoogleOAuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        var googleResult = await _googleOAuthService.ValidateGoogleTokenAsync(request.GoogleToken);
        if (!googleResult.IsValid)
        {
            throw new UnauthorizedAccessException("Invalid Google token");
        }

        var user = await GetOrCreateGoogleUser(googleResult);
        var response = _mapper.Map<GoogleOAuthResponse>(user);

        // Generate DocAI tokens
        response.DocaiToken = JwtUtil.GenerateJwtToken(user, _configuration);
        response.DocaiRefreshToken = JwtUtil.GenerateJwtRefreshToken(user, _configuration);

        // Set Google tokens
        response.GoogleAccessToken = googleResult.AccessToken;
        response.GoogleRefreshToken = googleResult.RefreshToken;

        // Store tokens in Redis
        await _redisService.SetDocAITokensAsync(user.Id.ToString(),
            response.DocaiToken, response.DocaiRefreshToken);
        await _redisService.SetGoogleTokensAsync(user.Id.ToString(),
            googleResult.AccessToken, googleResult.RefreshToken, googleResult.ExpiresAt);

        return response;
    }

    public async Task<GoogleOAuthResponse> GoogleCallbackAsync(string code, string state)
    {
        var request = new GoogleOAuthRequest { Code = code, State = state };
        return await _googleOAuthService.AuthenticateWithGoogleAsync(request);
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = JwtUtil.GetPrincipalFromExpiredToken(request.RefreshToken, _configuration);
        var userId = principal.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var user = await GetUserByIdAsync(Guid.Parse(userId));
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        var response = _mapper.Map<RefreshTokenResponse>(user);
        response.DocaiToken = JwtUtil.GenerateJwtToken(user, _configuration);
        response.DocaiRefreshToken = JwtUtil.GenerateJwtRefreshToken(user, _configuration);

        // Update DocAI tokens in Redis
        await _redisService.SetDocAITokensAsync(userId, response.DocaiToken, response.DocaiRefreshToken);

        // Refresh Google tokens if available
        var googleTokens = await _redisService.GetGoogleTokensAsync(userId);
        if (googleTokens.HasValue)
        {
            var refreshedGoogle = await _googleOAuthService.RefreshGoogleTokenAsync(googleTokens.Value.refreshToken);
            if (refreshedGoogle.HasValue)
            {
                response.GoogleAccessToken = refreshedGoogle.Value.accessToken;
                response.GoogleRefreshToken = refreshedGoogle.Value.refreshToken;

                await _redisService.SetGoogleTokensAsync(userId,
                    refreshedGoogle.Value.accessToken, refreshedGoogle.Value.refreshToken, DateTime.UtcNow.AddHours(1));
            }
        }

        return response;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId);

        if (user == null)
        {
            return false;
        }

        // Verify current password
        if (!PasswordUtil.VerifyPassword(request.CurrentPassword, user.Password))
        {
            return false;
        }

        // Update password
        user.Password = PasswordUtil.HashPassword(request.NewPassword);
        user.RequirePasswordChange = false;
        user.UpdateAt = DateTime.UtcNow;

        _unitOfWork.GetRepository<User>().UpdateAsync(user);
        var result = await _unitOfWork.CommitAsync();

        return result > 0;
    }

    private async Task<User> GetOrCreateGoogleUser(GoogleTokenValidationResult googleResult)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Email == googleResult.Email,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission));

        if (user == null)
        {
            // Create new user from Google account
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = googleResult.Email,
                FullName = googleResult.Name,
                GoogleId = googleResult.GoogleId,
                RequirePasswordChange = false,
                Password = PasswordUtil.HashPassword(Guid.NewGuid().ToString()), // Random password
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            };

            // Set default role and department
            var defaultRole = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
                predicate: r => r.RoleName == "Member");
            user.RoleId = defaultRole?.Id ?? Guid.Empty;

            var defaultDepartment = await _unitOfWork.GetRepository<Department>().SingleOrDefaultAsync(
                predicate: d => d.Name == "General");
            user.DepartmentId = defaultDepartment?.Id ?? Guid.Empty;

            await _unitOfWork.GetRepository<User>().InsertAsync(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created new user from Google OAuth: {Email}", googleResult.Email);
        }

        return user;
    }

    private async Task<User> GetUserByIdAsync(Guid userId)
    {
        return await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission));
    }

    public async Task<bool> RevokeGoogleTokenAsync(string userId)
    {
        try
        {
            // Revoke Google token through GoogleOAuthService
            var result = await _googleOAuthService.RevokeGoogleTokenAsync(userId);

            if (result)
            {
                _logger.LogInformation("Google token revoked successfully for user: {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Failed to revoke Google token for user: {UserId}", userId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking Google token for user: {UserId}", userId);
            return false;
        }
    }

    private async Task AssignPermissionsToUserAsync(Guid userId, List<Guid>? permissionIds)
    {
        var permissionsToAssign = new List<Guid>();

        if (permissionIds == null || !permissionIds.Any())
        {
            // Assign default permission: VIEW_OWN_DEPARTMENT_DOCUMENT
            permissionsToAssign.Add(Guid.Parse("e72214a0-24bc-471a-aca5-d897f4da0aad"));
        }
        else
        {
            permissionsToAssign = permissionIds;
        }

        foreach (var permissionId in permissionsToAssign)
        {
            var userPermission = new UserPermission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PermissionId = permissionId
            };

            await _unitOfWork.GetRepository<UserPermission>().InsertAsync(userPermission);
        }
    }

    public async Task<UserResponse> AdminUpdateUserAsync(Guid userId, AdminUpdateUserRequest request)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
        );

        if (user == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        // Validate unique fields if changed
        await ValidateUniqueFieldsForUpdateAsync(request.Email, request.Phone, userId);

        // Update basic fields
        if (!string.IsNullOrEmpty(request.FullName))
            user.FullName = request.FullName;
        if (!string.IsNullOrEmpty(request.Phone))
            user.Phone = request.Phone;
        if (!string.IsNullOrEmpty(request.Email))
            user.Email = request.Email;
        if (request.RoleId.HasValue)
            user.RoleId = request.RoleId.Value;
        if (request.DepartmentId.HasValue)
            user.DepartmentId = request.DepartmentId.Value;
        if (request.Active.HasValue)
            user.Active = request.Active.Value;
        if (request.RequirePasswordChange.HasValue)
            user.RequirePasswordChange = request.RequirePasswordChange.Value;

        user.UpdateAt = DateTime.UtcNow;

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Update user first
            _unitOfWork.GetRepository<User>().UpdateAsync(user);

            // Handle permissions separately to avoid tracking conflicts
            var existingPermissions = await _unitOfWork.GetRepository<UserPermission>()
                .GetListAsync(predicate: up => up.UserId == userId);

            // Remove existing permissions
            foreach (var permission in existingPermissions)
            {
                _unitOfWork.GetRepository<UserPermission>().DeleteAsync(permission);
            }

            // Add new permissions if provided
            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                var newUserPermissions = request.PermissionIds.Select(permissionId =>
                    new UserPermission
                    {
                        UserId = userId,
                        PermissionId = permissionId
                    }).ToList();

                foreach (var userPermission in newUserPermissions)
                {
                    await _unitOfWork.GetRepository<UserPermission>().InsertAsync(userPermission);
                }
            }

            var isSuccessful = await _unitOfWork.CommitAsync() > 0;
            if (!isSuccessful)
                throw new InvalidOperationException("Failed to update user");

            var response = await GetUserResponseAsync(userId);
            transaction.Complete();

            _logger.LogInformation("User {UserId} updated by Admin", userId);
            return response;
        }
        catch
        {
            throw;
        }
    }

    public async Task<UserResponse> UpdateUserProfileAsync(UserUpdateProfileRequest request)
    {
        var currentUserId = GetUserIdFromJwt();

        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == currentUserId
        );

        if (user == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        // Validate unique fields if changed
        await ValidateUniqueFieldsForUpdateAsync(request.Email, request.Phone, currentUserId);

        // Update only allowed fields
        if (!string.IsNullOrEmpty(request.FullName))
            user.FullName = request.FullName;
        if (!string.IsNullOrEmpty(request.Phone))
            user.Phone = request.Phone;
        if (!string.IsNullOrEmpty(request.Email))
            user.Email = request.Email;

        user.UpdateAt = DateTime.UtcNow;

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            _unitOfWork.GetRepository<User>().UpdateAsync(user);
            var isSuccessful = await _unitOfWork.CommitAsync() > 0;

            if (!isSuccessful)
                throw new InvalidOperationException("Failed to update profile");

            var response = await GetUserResponseAsync(currentUserId);
            transaction.Complete();

            _logger.LogInformation("User {UserId} updated profile", currentUserId);
            return response;
        }
        catch
        {
            throw;
        }
    }

    private async Task ValidateUniqueFieldsForUpdateAsync(string? email, string? phone, Guid excludeUserId)
    {
        var repo = _unitOfWork.GetRepository<User>();

        if (!string.IsNullOrEmpty(email))
        {
            var existingUser = await repo.SingleOrDefaultAsync(predicate: u => u.Email == email && u.Id != excludeUserId);
            if (existingUser != null)
                throw new BadHttpRequestException(MessageConstant.User.EmailExisted);
        }

        if (!string.IsNullOrEmpty(phone))
        {
            var existingUser = await repo.SingleOrDefaultAsync(predicate: u => u.Phone == phone && u.Id != excludeUserId);
            if (existingUser != null)
                throw new BadHttpRequestException(MessageConstant.User.PhoneNumberExisted);
        }
    }

    private async Task<UserResponse> GetUserResponseAsync(Guid userId)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission)
        );

        var userSetting = await _unitOfWork.GetRepository<UserSetting>()
            .SingleOrDefaultAsync(predicate: us => us.UserId == userId);

        var response = _mapper.Map<UserResponse>(user);

        if (userSetting != null)
        {
            response.UserSetting = new UserSettingResponse
            {
                Id = userSetting.Id,
                TwoFactorEnabled = userSetting.TwoFactorEnabled,
                TwoFactorMethod = userSetting.TwoFactorMethod,
                NotificationsEnabled = userSetting.NotificationsEnabled,
                UpdateAt = userSetting.UpdateAt
            };
        }

        return response;
    }

    public async Task<UserSettingResponse> UpdateUserSettingAsync(UpdateUserSettingRequest request)
    {
        var currentUserId = GetUserIdFromJwt();

        // Validate TwoFactorMethod if provided
        if (!string.IsNullOrEmpty(request.TwoFactorMethod) &&
            !request.TwoFactorMethod.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException("TwoFactorMethod chỉ hỗ trợ 'Email'");
        }

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            var userSetting = await _unitOfWork.GetRepository<UserSetting>()
                .SingleOrDefaultAsync(predicate: us => us.UserId == currentUserId);

            if (userSetting == null)
            {
                // Create new UserSetting if not exists
                userSetting = new UserSetting
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    TwoFactorEnabled = request.TwoFactorEnabled,
                    TwoFactorMethod = request.TwoFactorMethod ?? "Email",
                    NotificationsEnabled = request.NotificationsEnabled,
                    UpdateAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<UserSetting>().InsertAsync(userSetting);
            }
            else
            {
                // Update existing UserSetting
                userSetting.TwoFactorEnabled = request.TwoFactorEnabled;
                userSetting.TwoFactorMethod = request.TwoFactorMethod ?? userSetting.TwoFactorMethod;
                userSetting.NotificationsEnabled = request.NotificationsEnabled;
                userSetting.UpdateAt = DateTime.UtcNow;

                _unitOfWork.GetRepository<UserSetting>().UpdateAsync(userSetting);
            }

            var isSuccessful = await _unitOfWork.CommitAsync() > 0;
            if (!isSuccessful)
                throw new InvalidOperationException("Failed to update user settings");

            var response = new UserSettingResponse
            {
                Id = userSetting.Id,
                TwoFactorEnabled = userSetting.TwoFactorEnabled,
                TwoFactorMethod = userSetting.TwoFactorMethod,
                NotificationsEnabled = userSetting.NotificationsEnabled,
                UpdateAt = userSetting.UpdateAt
            };

            transaction.Complete();
            _logger.LogInformation("User {UserId} updated settings", currentUserId);

            return response;
        }
        catch
        {
            throw;
        }
    }

    public async Task<UserResponse> GetUserByIdAminAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new BadHttpRequestException("UserId không hợp lệ");

        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId,
            include: u => u.Include(x => x.Role)
                         .Include(x => x.Department)
                         .Include(x => x.UserPermissions)
                         .ThenInclude(up => up.Permission)
        );

        if (user == null)
            throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

        var userSetting = await _unitOfWork.GetRepository<UserSetting>()
            .SingleOrDefaultAsync(predicate: us => us.UserId == userId);

        var response = _mapper.Map<UserResponse>(user);

        if (userSetting != null)
        {
            response.UserSetting = new UserSettingResponse
            {
                Id = userSetting.Id,
                TwoFactorEnabled = userSetting.TwoFactorEnabled,
                TwoFactorMethod = userSetting.TwoFactorMethod,
                NotificationsEnabled = userSetting.NotificationsEnabled,
                UpdateAt = userSetting.UpdateAt
            };
        }

        _logger.LogInformation("Admin retrieved user information for UserId: {UserId}", userId);
        return response;
    }


    public async Task<List<string>> GetDepartmentEmployeeEmailsAsync(string departmentId)
    {
        try
        {
            _logger.LogInformation("Getting employee emails for department {DepartmentId}", departmentId);

            if (!Guid.TryParse(departmentId, out var deptGuid))
            {
                _logger.LogWarning("Invalid department ID format: {DepartmentId}", departmentId);
                return new List<string>();
            }

            var users = await _unitOfWork.GetRepository<User>().GetListAsync(
                predicate: u => u.DepartmentId == deptGuid && u.Active,
                selector: u => u.Email
            );

            _logger.LogInformation("Retrieved {Count} employee emails for department {DepartmentId}", users.Count, departmentId);
            return users.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department employee emails for {DepartmentId}", departmentId);
            return new List<string>();
        }
    }

    public async Task<List<string>> GetDepartmentManagerEmailsAsync(string departmentId)
    {
        try
        {
            _logger.LogInformation("Getting manager emails for department {DepartmentId}", departmentId);

            if (!Guid.TryParse(departmentId, out var deptGuid))
            {
                _logger.LogWarning("Invalid department ID format: {DepartmentId}", departmentId);
                return new List<string>();
            }

            // Get users who are managers in the department
            // Assuming managers have a specific role - you may need to adjust this logic
            var users = await _unitOfWork.GetRepository<User>().GetListAsync(
                predicate: u => u.DepartmentId == deptGuid && u.Active,
                include: u => u.Include(x => x.Role),
                selector: u => new { u.Email, u.Role.RoleName }
            );

            // Filter for manager roles - adjust role names as needed
            var managerEmails = users
                .Where(u => u.RoleName != null && (u.RoleName.ToLower().Contains("manager") || u.RoleName.ToLower().Contains("lead")))
                .Select(u => u.Email)
                .ToList();

            _logger.LogInformation("Retrieved {Count} manager emails for department {DepartmentId}", managerEmails.Count, departmentId);
            return managerEmails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department manager emails for {DepartmentId}", departmentId);
            return new List<string>();
        }
    }

    public async Task<List<string>> GetAllCompanyEmployeeEmailsAsync()
    {
        try
        {
            _logger.LogInformation("Getting all company employee emails");

            var emails = await _unitOfWork.GetRepository<User>().GetListAsync(
                predicate: u => u.Active,
                selector: u => u.Email
            );

            _logger.LogInformation("Retrieved {Count} company employee emails", emails.Count);
            return emails.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all company employee emails");
            return new List<string>();
        }
    }

    public async Task<string?> GetUserEmailByIdAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Getting email for user {UserId}", userId);

            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogWarning("Invalid user ID format: {UserId}", userId);
                return null;
            }

            var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
                predicate: u => u.Id == userGuid && u.Active,
                selector: u => u.Email
            );

            if (user != null)
            {
                _logger.LogInformation("Retrieved email for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("User {UserId} not found or inactive", userId);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email for user {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> ResetPasswordByEmailAsync(ResetPasswordByEmailRequest request)
    {
        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Email == request.Email
            );
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        user.Password = PasswordUtil.HashPassword(request.Password);
        user.UpdateAt = DateTime.UtcNow;

        _unitOfWork.GetRepository<User>().UpdateAsync(user);
        var result = await _unitOfWork.CommitAsync();

        return result > 0;
    }

}
