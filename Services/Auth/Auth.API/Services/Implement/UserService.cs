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
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Interface;

public class UserService : BaseService<UserService>, IUserService
{
    private readonly IRedisService _redisService;
    private IConfiguration _configuration;

    public UserService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<UserService> logger,
        IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper,
        IRedisService redisService) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
    {
        _configuration = configuration;
        _redisService = redisService;
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
        // Lấy IQueryable từ repository, sau đó áp dụng AsNoTracking() và các Include
        var query = _unitOfWork.GetRepository<User>().GetQuery()
            .AsNoTracking() // <<< Lỗi này sẽ được giải quyết vì GetQuery() trả về IQueryable
            .Where(u => u.UserName == username)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserDepartments)
            .ThenInclude(ud => ud.Department)
            .Include(u => u.DepartmentRolePermissions)
            .ThenInclude(d => d.Department)
            .Include(u => u.DepartmentRolePermissions)
            .ThenInclude(r => r.Role)
            .Include(u => u.DepartmentRolePermissions)
            .ThenInclude(p => p.Permission);

        return await query.SingleOrDefaultAsync(); // Gọi SingleOrDefaultAsync trên IQueryable
    }

    private async Task<LoginResponse> CreateLoginResponse(User user)
    {
        // 1. Lấy Contextual Permissions TRỰC TIẾP từ user.DepartmentRolePermissions
        // Đảm bảo Distinct để tránh các claim trùng lặp nếu có lỗi dữ liệu hoặc logic.
        var contextualPermissions = user.DepartmentRolePermissions
            ?.Select(drp => new ContextualPermissionClaim
            {
                DeptId = drp.DepartmentId,
                DeptName = drp.Department.Name,
                RoleName = drp.Role.RoleName,
                PermissionName = drp.Permission.Name,
            })
            .Distinct() // Rất quan trọng để tránh trùng lặp trong JWT
            .ToList() ?? new List<ContextualPermissionClaim>();

        // 2. Xác định General Roles (ví dụ: "Admin")
        // Các vai trò này thường không gắn với DepartmentId cụ thể.
        // Lấy từ UserRoles của user.
        var generalRoles = user.UserRoles
            ?.Where(ur => ur.Role?.RoleName == "Admin") // Giả định "Admin" là general role
            .Select(ur => ur.Role.RoleName)
            .ToList() ?? new List<string>();

        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.UserName,
            Email = user.Email,
            FullName = user.FullName, // Thêm FullName

            // Dữ liệu hiển thị (có thể giữ lại nếu frontend cần)
            Roles = user.UserRoles?.Select(ur => new RoleResponse
            {
                RoleName = ur.Role.RoleName,
                Description = ur.Role.Description,
                CreateAt = ur.Role.CreateAt,
                UpdateAt = ur.Role.UpdateAt
            }).ToList() ?? new List<RoleResponse>(),

            Departments = user.UserDepartments?.Select(ud => new DepartmentResponse
            {
                Name = ud.Department.Name, // Dùng Department.Name
                Description = ud.Department.Description,
                CreateAt = ud.Department.CreateAt,
                UpdateAt = ud.Department.UpdateAt
            }).ToList() ?? new List<DepartmentResponse>(),

            // Dữ liệu chính cho JWT
            ContextualPermissions = contextualPermissions,
            GeneralRoles = generalRoles,

            Token = JwtUtil.GenerateJwtToken(user, contextualPermissions, generalRoles, _configuration),
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
        var roleName = await GetRoleNameFromActivationCodeAsync(request.ActivationCode);
        var role = await GetRoleByNameAsync(roleName);
        var deparment = await GetDepartmentByIdAsync(request.DepartmentId);

        var userDepartment = new UserDepartment()
        {
            Id = Guid.NewGuid(),
            DepartmentId = deparment.Id,
            UserId = user.Id,
        };
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            UserId = user.Id
        };

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                await _unitOfWork.GetRepository<User>().InsertAsync(user);
                await _unitOfWork.GetRepository<UserRole>().InsertAsync(userRole);
                await _unitOfWork.GetRepository<UserDepartment>().InsertAsync(userDepartment);

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
                    throw new InvalidOperationException("Failed to save user and role");

                transaction.Complete();

                return null;
                // return CreateRegisterResponse(user);
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

    private async Task<string> GetRoleNameFromActivationCodeAsync(string activationCode)
    {
        if (string.IsNullOrWhiteSpace(activationCode))
            return "member";

        var activation = await _unitOfWork.GetRepository<ActiveKey>()
            .SingleOrDefaultAsync(predicate: u => u.ActivationCode == activationCode);

        if (activation == null)
            throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

        return activation.RoleName;
    }

    private async Task<Department> GetDepartmentByIdAsync(Guid departmentId)
    {
        var deparment = await _unitOfWork.GetRepository<Department>()
            .SingleOrDefaultAsync(predicate: d => d.Id == departmentId);
        if (deparment == null)
            throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);
        return deparment;
    }

    private async Task<Role> GetRoleByNameAsync(string roleName)
    {
        var role = await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.RoleName.ToLowerInvariant() == roleName.ToLowerInvariant());

        if (role == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        return role;
    }

    // private RegisterResponse CreateRegisterResponse(User user)
    // {
    //     var response = _mapper.Map<RegisterResponse>(user);
    //     response.Roles = user.UserRoles?.Select(ur => new RoleResponse
    //     {
    //         RoleName = ur.Role.RoleName,
    //         Description = ur.Role.Description,
    //         CreateAt = ur.Role.CreateAt,
    //         UpdateAt = ur.Role.UpdateAt
    //     }).ToList() ?? new List<RoleResponse>();
    //     response.Departments = user.UserDepartments?.Select(ud => new DepartmentResponse
    //     {
    //         Name = ud.Department.Name,
    //         Description = ud.Department.Description,
    //         CreateAt = ud.Department.CreateAt,
    //         UpdateAt = ud.Department.UpdateAt
    //     }).ToList() ?? new List<DepartmentResponse>();
    //     response.Token = JwtUtil.GenerateJwtToken(user, new Tuple<string, Guid>("userId", user.Id), _configuration);
    //     response.RefreshToken = JwtUtil.GenerateRefreshToken();
    //     return response;
    // }

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

    public async Task<ActiveKeyResponse> CreateActiveKeyAsync(ActiveKeyRequest request)
    {
        var userId = GetUserIdFromJwt();

        var user = await _unitOfWork.GetRepository<User>().SingleOrDefaultAsync(
            predicate: u => u.Id == userId,
            include: u => u.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
        );

        if (user == null)
            throw new UnauthorizedAccessException(MessageConstant.User.UserNotFound);

        var currentUserRole = user.UserRoles?.FirstOrDefault()?.Role;

        if (currentUserRole == null)
            throw new UnauthorizedAccessException(MessageConstant.User.UserNotHaveRole);

        var targetRole = await _unitOfWork.GetRepository<Role>()
            .SingleOrDefaultAsync(predicate: r => r.Id == request.RoleId);

        if (targetRole == null)
            throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

        // Enum parsing (based on RoleName or RoleId)
        var currentRoleLevel = ParseRole(currentUserRole.RoleName);
        var targetRoleLevel = ParseRole(targetRole.RoleName);

        if (currentRoleLevel >= targetRoleLevel)
            throw new BadHttpRequestException("You do not have permission to create this ActiveKey");

        var code = await GenerateActivationCode();

        var activeKey = new ActiveKey
        {
            Id = Guid.NewGuid(),
            ActivationCode = code,
            RoleName = targetRole.RoleName
        };

        await _unitOfWork.GetRepository<ActiveKey>().InsertAsync(activeKey);
        await _unitOfWork.CommitAsync();

        return new ActiveKeyResponse
        {
            ActivationCode = code,
            RoleName = targetRole.RoleName
        };
    }

    private RoleLevel ParseRole(string roleName)
    {
        return roleName switch
        {
            "Admin" => RoleLevel.Admin,
            "Director" => RoleLevel.Director,
            "Manager" => RoleLevel.Manager,
            "Editor" => RoleLevel.Editor,
            _ => throw new ArgumentException("Unknown role name")
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