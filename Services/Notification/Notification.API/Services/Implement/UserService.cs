using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Shared.Command;
using Shared.DTOs;

namespace Notification.API.Services.Implement
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IRequestClient<GetDepartmentUsersCommand> _departmentUsersClient;
        private readonly IRequestClient<GetUsersByRoleCommand> _usersByRoleClient;
        private readonly IRequestClient<GetDocumentStakeholdersCommand> _documentStakeholdersClient;
        private readonly IRequestClient<GetUserByIdCommand> _getUserByIdClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public UserService(
            ILogger<UserService> logger,
            IRequestClient<GetDepartmentUsersCommand> departmentUsersClient,
            IRequestClient<GetUsersByRoleCommand> usersByRoleClient,
            IRequestClient<GetDocumentStakeholdersCommand> documentStakeholdersClient,
            IRequestClient<GetUserByIdCommand> getUserByIdClient,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _logger = logger;
            _departmentUsersClient = departmentUsersClient;
            _usersByRoleClient = usersByRoleClient;
            _documentStakeholdersClient = documentStakeholdersClient;
            _getUserByIdClient = getUserByIdClient;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task<List<UserInfo>> GetDepartmentManagersAsync(Guid departmentId)
        {
            var cacheKey = $"dept_managers_{departmentId}";
            if (_cache.TryGetValue(cacheKey, out List<UserInfo>? cached) && cached != null)
                return cached;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _departmentUsersClient.GetResponse<GetDepartmentUsersResponse>(
                    new GetDepartmentUsersCommand
                    {
                        DepartmentId = departmentId,
                        RoleFilter = "Manager"
                    },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    var managers = response.Message.Users.Select(u => new UserInfo
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        Name = u.Name,
                        Department = u.DepartmentName
                    }).ToList();

                    _cache.Set(cacheKey, managers, TimeSpan.FromMinutes(5));
                    return managers;
                }

                _logger.LogWarning("Failed to get department managers for {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserInfo>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department managers for {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department managers for {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
        }

        public async Task<List<UserInfo>> GetDepartmentEditorsAsync(Guid departmentId)
        {
            var cacheKey = $"dept_editors_{departmentId}";
            if (_cache.TryGetValue(cacheKey, out List<UserInfo>? cached) && cached != null)
                return cached;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _departmentUsersClient.GetResponse<GetDepartmentUsersResponse>(
                    new GetDepartmentUsersCommand
                    {
                        DepartmentId = departmentId,
                        RoleFilter = "Editor"
                    },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    var editors = response.Message.Users.Select(u => new UserInfo
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        Name = u.Name,
                        Department = u.DepartmentName
                    }).ToList();

                    _cache.Set(cacheKey, editors, TimeSpan.FromMinutes(5));
                    return editors;
                }

                _logger.LogWarning("Failed to get department editors for {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserInfo>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department editors for {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department editors for {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
        }

        public async Task<List<UserInfo>> GetDocumentStakeholdersAsync(Guid documentId)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                var response = await _documentStakeholdersClient.GetResponse<GetDocumentStakeholdersResponse>(
                    new GetDocumentStakeholdersCommand { DocumentId = documentId },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    return response.Message.Stakeholders.Select(s => new UserInfo
                    {
                        UserId = s.UserId,
                        Email = s.Email,
                        Name = s.Name,
                        Department = s.DepartmentName
                    }).ToList();
                }

                _logger.LogWarning("Failed to get document stakeholders for {DocumentId}: {Error}",
                    documentId, response.Message.ErrorMessage);
                return new List<UserInfo>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting document stakeholders for {DocumentId}", documentId);
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document stakeholders for {DocumentId}", documentId);
                return new List<UserInfo>();
            }
        }

        public async Task<UserInfo?> GetUserByIdAsync(Guid userId)
        {
            var cacheKey = $"user_{userId}";
            if (_cache.TryGetValue(cacheKey, out UserInfo? cached) && cached != null)
                return cached;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _getUserByIdClient.GetResponse<GetUserByIdResponse>(
                    new GetUserByIdCommand { UserId = userId },
                    timeout.Token
                );

                if (response.Message.Success && response.Message.User != null)
                {
                    var user = new UserInfo
                    {
                        UserId = response.Message.User.UserId,
                        Email = response.Message.User.Email,
                        Name = response.Message.User.Name,
                        Department = response.Message.User.DepartmentName
                    };

                    _cache.Set(cacheKey, user, TimeSpan.FromMinutes(15));
                    return user;
                }

                return null;
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting user {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", userId);
                return null;
            }
        }

        public async Task<List<UserInfo>> GetUsersByRoleAsync(string roleName)
        {
            var cacheKey = $"users_role_{roleName.ToLower()}";
            if (_cache.TryGetValue(cacheKey, out List<UserInfo>? cached) && cached != null)
                return cached;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _usersByRoleClient.GetResponse<GetUsersByRoleResponse>(
                    new GetUsersByRoleCommand { RoleName = roleName },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    var users = response.Message.Users.Select(u => new UserInfo
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        Name = u.Name,
                        Department = u.DepartmentName
                    }).ToList();

                    _cache.Set(cacheKey, users, TimeSpan.FromMinutes(10));
                    return users;
                }

                _logger.LogWarning("Failed to get users by role {RoleName}: {Error}",
                    roleName, response.Message.ErrorMessage);
                return new List<UserInfo>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting users by role {RoleName}", roleName);
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role {RoleName}", roleName);
                return new List<UserInfo>();
            }
        }

        public async Task<List<UserInfo>> GetUsersByDepartmentAsync(Guid departmentId)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _departmentUsersClient.GetResponse<GetDepartmentUsersResponse>(
                    new GetDepartmentUsersCommand { DepartmentId = departmentId },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    return response.Message.Users.Select(u => new UserInfo
                    {
                        UserId = u.UserId,
                        Email = u.Email,
                        Name = u.Name,
                        Department = u.DepartmentName
                    }).ToList();
                }

                _logger.LogWarning("Failed to get users by department {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserInfo>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting users by department {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by department {DepartmentId}", departmentId);
                return new List<UserInfo>();
            }
        }
    }
}
