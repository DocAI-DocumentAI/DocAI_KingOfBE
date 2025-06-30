using System.Security.Cryptography;
using System.Text;
using Auth.API.Constants;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.ActiveKey;
using Auth.API.Payload.Response.Staff;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Auth.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services.Implement
{
    public class ActiveKeyService : BaseService<ActiveKeyService>, IActiveKeyService
    {
        public ActiveKeyService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<ActiveKeyService> logger,
            IHttpContextAccessor httpContextAccessor, IConfiguration configuration, IMapper mapper)
            : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
        {
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

        public async Task<IPaginate<ActiveKeyListResponse>> GetAllActiveKeysAsync(int page, int size, ActiveKeyFilter? filter, string? sortBy, bool isAsc)
        {
            var activeKeys = await _unitOfWork.GetRepository<ActiveKey>().GetPagingListAsync(
                selector: s => new ActiveKey()
                {
                    Id = s.Id,
                    ActivationCode = s.ActivationCode,
                    Status = s.Status,
                    RoleId = s.RoleId,
                    DepartmentId = s.DepartmentId,
                    CreatedByUserId = s.CreatedByUserId,
                    UsedByUserId = s.UsedByUserId,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    Role = s.Role,
                    Department = s.Department,
                    CreatedByUser = s.CreatedByUser,
                    UsedByUser = s.UsedByUser
                },
                page: page,
                size: size,
                filter: filter,
                sortBy: sortBy,
                isAsc: isAsc,
                include: s => s.Include(ak => ak.Role)
                              .Include(ak => ak.Department)
                              .Include(ak => ak.CreatedByUser)
                              .Include(ak => ak.UsedByUser)
            );

            // Map to response
            var response = new Paginate<ActiveKeyListResponse>
            {
                Page = activeKeys.Page,
                Size = activeKeys.Size,
                Total = activeKeys.Total,
                TotalPages = activeKeys.TotalPages,
                Items = activeKeys.Items.Select(ak => new ActiveKeyListResponse
                {
                    Id = ak.Id,
                    ActivationCode = ak.ActivationCode,
                    Status = ak.Status,
                    Role = new RoleResponse
                    {
                        Id = ak.Role.Id,
                        RoleName = ak.Role.RoleName,
                        Description = ak.Role.Description,
                        CreateAt = ak.Role.CreateAt,
                        UpdateAt = ak.Role.UpdateAt
                    },
                    Department = new DepartmentResponse
                    {
                        Id = ak.Department.Id,
                        Name = ak.Department.Name,
                        Description = ak.Department.Description,
                        CreateAt = ak.Department.CreateAt,
                        UpdateAt = ak.Department.UpdateAt
                    },
                    CreatedByUser = ak.CreatedByUser != null ? new UserSummaryResponse
                    {
                        Id = ak.CreatedByUser.Id,
                        UserName = ak.CreatedByUser.UserName,
                        FullName = ak.CreatedByUser.FullName,
                        Email = ak.CreatedByUser.Email
                    } : null,
                    UsedByUser = ak.UsedByUser != null ? new UserSummaryResponse
                    {
                        Id = ak.UsedByUser.Id,
                        UserName = ak.UsedByUser.UserName,
                        FullName = ak.UsedByUser.FullName,
                        Email = ak.UsedByUser.Email
                    } : null,
                    CreatedAt = ak.CreatedAt,
                    UpdatedAt = ak.UpdatedAt
                }).ToList()
            };

            return response;
        }

        public async Task<ActiveKeyListResponse> GetActiveKeyByIdAsync(Guid id)
        {
            var activeKey = await _unitOfWork.GetRepository<ActiveKey>()
                .SingleOrDefaultAsync(
                    predicate: ak => ak.Id == id,
                    include: s => s.Include(ak => ak.Role)
                                  .Include(ak => ak.Department)
                                  .Include(ak => ak.CreatedByUser)
                                  .Include(ak => ak.UsedByUser)
                );

            if (activeKey == null)
                throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

            // Map to response
            var response = new ActiveKeyListResponse
            {
                Id = activeKey.Id,
                ActivationCode = activeKey.ActivationCode,
                Status = activeKey.Status,
                Role = new RoleResponse
                {
                    Id = activeKey.Role.Id,
                    RoleName = activeKey.Role.RoleName,
                    Description = activeKey.Role.Description,
                    CreateAt = activeKey.Role.CreateAt,
                    UpdateAt = activeKey.Role.UpdateAt
                },
                Department = new DepartmentResponse
                {
                    Id = activeKey.Department.Id,
                    Name = activeKey.Department.Name,
                    Description = activeKey.Department.Description,
                    CreateAt = activeKey.Department.CreateAt,
                    UpdateAt = activeKey.Department.UpdateAt
                },
                CreatedByUser = activeKey.CreatedByUser != null ? new UserSummaryResponse
                {
                    Id = activeKey.CreatedByUser.Id,
                    UserName = activeKey.CreatedByUser.UserName,
                    FullName = activeKey.CreatedByUser.FullName,
                    Email = activeKey.CreatedByUser.Email
                } : null,
                UsedByUser = activeKey.UsedByUser != null ? new UserSummaryResponse
                {
                    Id = activeKey.UsedByUser.Id,
                    UserName = activeKey.UsedByUser.UserName,
                    FullName = activeKey.UsedByUser.FullName,
                    Email = activeKey.UsedByUser.Email
                } : null,
                CreatedAt = activeKey.CreatedAt,
                UpdatedAt = activeKey.UpdatedAt
            };

            return response;
        }

        public async Task<ActiveKeyListResponse> UpdateActiveKeyAsync(Guid id, UpdateActiveKeyRequest request)
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

            // Lấy thông tin ActiveKey cần update
            var activeKey = await _unitOfWork.GetRepository<ActiveKey>()
                .SingleOrDefaultAsync(
                    predicate: ak => ak.Id == id,
                    include: s => s.Include(ak => ak.Role)
                                  .Include(ak => ak.Department)
                                  .Include(ak => ak.CreatedByUser)
                                  .Include(ak => ak.UsedByUser)
                );

            if (activeKey == null)
                throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

            // Kiểm tra quyền: chỉ người tạo hoặc Admin mới được update
            bool isAdmin = currentUser.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            bool isCreator = activeKey.CreatedByUserId == currentUserId;

            if (!isAdmin && !isCreator)
                throw new UnauthorizedAccessException("You can only update activation codes that you created or you must be an Admin");

            // Kiểm tra nếu ActiveKey đã được sử dụng thì không được phép update
            if (activeKey.UsedByUserId != null)
                throw new BadHttpRequestException("Cannot update an activation code that has already been used");

            // Validate role mới
            var newRole = await _unitOfWork.GetRepository<Role>()
                .SingleOrDefaultAsync(predicate: r => r.Id == request.RoleId);

            if (newRole == null)
                throw new BadHttpRequestException(MessageConstant.Role.RoleNotFound);

            // Validate department mới
            var newDepartment = await _unitOfWork.GetRepository<Department>()
                .SingleOrDefaultAsync(predicate: d => d.Id == request.DepartmentId);

            if (newDepartment == null)
                throw new BadHttpRequestException(MessageConstant.Department.DepartmentNotFound);

            // Kiểm tra quyền dựa trên role hierarchy (không được tạo role cao hơn mình)
            int currentUserRoleLevel = ParseRole(currentUser.Role.RoleName);
            int newRoleLevel = ParseRole(newRole.RoleName);

            if (currentUserRoleLevel <= newRoleLevel)
                throw new UnauthorizedAccessException($"You cannot update to role '{newRole.RoleName}' because your role level is not high enough");

            // Kiểm tra quyền dựa trên department (nếu không phải admin)
            if (!isAdmin && currentUser.DepartmentId != request.DepartmentId)
                throw new UnauthorizedAccessException("You can only update activation codes for your own department");

            // Cập nhật thông tin
            activeKey.Status = request.Status;
            activeKey.RoleId = request.RoleId;
            activeKey.DepartmentId = request.DepartmentId;
            activeKey.UpdatedAt = DateTime.UtcNow;

            // Lưu vào database
            _unitOfWork.GetRepository<ActiveKey>().UpdateAsync(activeKey);
            var isSuccessful = await _unitOfWork.CommitAsync() > 0;

            if (!isSuccessful)
                throw new InvalidOperationException("Failed to update activation code");

            // Lấy lại ActiveKey với thông tin mới để trả về
            var updatedActiveKey = await _unitOfWork.GetRepository<ActiveKey>()
                .SingleOrDefaultAsync(
                    predicate: ak => ak.Id == id,
                    include: s => s.Include(ak => ak.Role)
                                  .Include(ak => ak.Department)
                                  .Include(ak => ak.CreatedByUser)
                                  .Include(ak => ak.UsedByUser)
                );

            // Map to response
            var response = new ActiveKeyListResponse
            {
                Id = updatedActiveKey.Id,
                ActivationCode = updatedActiveKey.ActivationCode,
                Status = updatedActiveKey.Status,
                Role = new RoleResponse
                {
                    Id = updatedActiveKey.Role.Id,
                    RoleName = updatedActiveKey.Role.RoleName,
                    Description = updatedActiveKey.Role.Description,
                    CreateAt = updatedActiveKey.Role.CreateAt,
                    UpdateAt = updatedActiveKey.Role.UpdateAt
                },
                Department = new DepartmentResponse
                {
                    Id = updatedActiveKey.Department.Id,
                    Name = updatedActiveKey.Department.Name,
                    Description = updatedActiveKey.Department.Description,
                    CreateAt = updatedActiveKey.Department.CreateAt,
                    UpdateAt = updatedActiveKey.Department.UpdateAt
                },
                CreatedByUser = updatedActiveKey.CreatedByUser != null ? new UserSummaryResponse
                {
                    Id = updatedActiveKey.CreatedByUser.Id,
                    UserName = updatedActiveKey.CreatedByUser.UserName,
                    FullName = updatedActiveKey.CreatedByUser.FullName,
                    Email = updatedActiveKey.CreatedByUser.Email
                } : null,
                UsedByUser = updatedActiveKey.UsedByUser != null ? new UserSummaryResponse
                {
                    Id = updatedActiveKey.UsedByUser.Id,
                    UserName = updatedActiveKey.UsedByUser.UserName,
                    FullName = updatedActiveKey.UsedByUser.FullName,
                    Email = updatedActiveKey.UsedByUser.Email
                } : null,
                CreatedAt = updatedActiveKey.CreatedAt,
                UpdatedAt = updatedActiveKey.UpdatedAt
            };

            return response;
        }

        public async Task<ActiveKeyListResponse> DeleteActiveKeyAsync(Guid id)
        {
            // Lấy thông tin user hiện tại từ token
            var currentUserId = GetUserIdFromJwt();
            var currentUser = await _unitOfWork.GetRepository<User>()
                .SingleOrDefaultAsync(predicate: u => u.Id == currentUserId,
                                     include: u => u.Include(u => u.Role).Include(u => u.Department));

            if (currentUser == null)
                throw new BadHttpRequestException(MessageConstant.User.UserNotFound);

            // Lấy thông tin ActiveKey cần xóa với đầy đủ thông tin liên quan
            var activeKey = await _unitOfWork.GetRepository<ActiveKey>()
                .SingleOrDefaultAsync(
                    predicate: ak => ak.Id == id,
                    include: s => s.Include(ak => ak.Role)
                                  .Include(ak => ak.Department)
                                  .Include(ak => ak.CreatedByUser)
                                  .Include(ak => ak.UsedByUser)
                );

            if (activeKey == null)
                throw new BadHttpRequestException(MessageConstant.ActivationCode.ActivationcodeNotFound);

            // Kiểm tra quyền: chỉ người tạo hoặc Admin mới được xóa
            bool isAdmin = currentUser.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            bool isCreator = activeKey.CreatedByUserId == currentUserId;

            if (!isAdmin && !isCreator)
                throw new UnauthorizedAccessException("You can only delete activation codes that you created or you must be an Admin");

            // Kiểm tra nếu ActiveKey đã được sử dụng thì không được phép xóa
            if (activeKey.UsedByUserId != null)
                throw new BadHttpRequestException("Cannot delete an activation code that has already been used");

            // Tạo response trước khi xóa
            var response = new ActiveKeyListResponse
            {
                Id = activeKey.Id,
                ActivationCode = activeKey.ActivationCode,
                Status = activeKey.Status,
                Role = new RoleResponse
                {
                    Id = activeKey.Role.Id,
                    RoleName = activeKey.Role.RoleName,
                    Description = activeKey.Role.Description,
                    CreateAt = activeKey.Role.CreateAt,
                    UpdateAt = activeKey.Role.UpdateAt
                },
                Department = new DepartmentResponse
                {
                    Id = activeKey.Department.Id,
                    Name = activeKey.Department.Name,
                    Description = activeKey.Department.Description,
                    CreateAt = activeKey.Department.CreateAt,
                    UpdateAt = activeKey.Department.UpdateAt
                },
                CreatedByUser = activeKey.CreatedByUser != null ? new UserSummaryResponse
                {
                    Id = activeKey.CreatedByUser.Id,
                    UserName = activeKey.CreatedByUser.UserName,
                    FullName = activeKey.CreatedByUser.FullName,
                    Email = activeKey.CreatedByUser.Email
                } : null,
                UsedByUser = activeKey.UsedByUser != null ? new UserSummaryResponse
                {
                    Id = activeKey.UsedByUser.Id,
                    UserName = activeKey.UsedByUser.UserName,
                    FullName = activeKey.UsedByUser.FullName,
                    Email = activeKey.UsedByUser.Email
                } : null,
                CreatedAt = activeKey.CreatedAt,
                UpdatedAt = activeKey.UpdatedAt
            };

            // Xóa ActiveKey
            _unitOfWork.GetRepository<ActiveKey>().DeleteAsync(activeKey);
            var isSuccessful = await _unitOfWork.CommitAsync() > 0;

            if (!isSuccessful)
                throw new InvalidOperationException("Failed to delete activation code");

            return response;
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
    }
}
