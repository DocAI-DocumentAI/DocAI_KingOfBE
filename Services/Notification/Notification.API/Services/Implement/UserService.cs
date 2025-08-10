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

        public UserService(
            ILogger<UserService> logger,
            IRequestClient<GetDepartmentUsersCommand> departmentUsersClient,
            IRequestClient<GetUsersByRoleCommand> usersByRoleClient,
            IRequestClient<GetDocumentStakeholdersCommand> documentStakeholdersClient,
            IRequestClient<GetUserByIdCommand> getUserByIdClient,
            IMemoryCache cache)
        {
            _logger = logger;
            _departmentUsersClient = departmentUsersClient;
            _usersByRoleClient = usersByRoleClient;
            _documentStakeholdersClient = documentStakeholdersClient;
            _getUserByIdClient = getUserByIdClient;
            _cache = cache;
        }

        public async Task<List<UserDto>> GetDepartmentManagersAsync(Guid departmentId)
        {
            var cacheKey = $"dept_managers_{departmentId}";
            if (_cache.TryGetValue(cacheKey, out List<UserDto>? cached) && cached != null)
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
                    _cache.Set(cacheKey, response.Message.Users, TimeSpan.FromMinutes(5));
                    return response.Message.Users;
                }

                _logger.LogWarning("Failed to get department managers for {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department managers for {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department managers for {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
        }

        public async Task<List<UserDto>> GetDepartmentEditorsAsync(Guid departmentId)
        {
            var cacheKey = $"dept_editors_{departmentId}";
            if (_cache.TryGetValue(cacheKey, out List<UserDto>? cached) && cached != null)
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
                    _cache.Set(cacheKey, response.Message.Users, TimeSpan.FromMinutes(5));
                    return response.Message.Users;
                }

                _logger.LogWarning("Failed to get department editors for {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department editors for {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department editors for {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
        }

        public async Task<List<UserDto>> GetDocumentStakeholdersAsync(string documentId)
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
                    return response.Message.Stakeholders;
                }

                _logger.LogWarning("Failed to get document stakeholders for {DocumentId}: {Error}",
                    documentId, response.Message.ErrorMessage);
                return new List<UserDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting document stakeholders for {DocumentId}", documentId);
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document stakeholders for {DocumentId}", documentId);
                return new List<UserDto>();
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid userId)
        {
            var cacheKey = $"user_{userId}";
            if (_cache.TryGetValue(cacheKey, out UserDto? cached) && cached != null)
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
                    _cache.Set(cacheKey, response.Message.User, TimeSpan.FromMinutes(15));
                    return response.Message.User;
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

        public async Task<List<UserDto>> GetUsersByRoleAsync(string roleName)
        {
            var cacheKey = $"users_role_{roleName.ToLower()}";
            if (_cache.TryGetValue(cacheKey, out List<UserDto>? cached) && cached != null)
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
                    _cache.Set(cacheKey, response.Message.Users, TimeSpan.FromMinutes(10));
                    return response.Message.Users;
                }

                _logger.LogWarning("Failed to get users by role {RoleName}: {Error}",
                    roleName, response.Message.ErrorMessage);
                return new List<UserDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting users by role {RoleName}", roleName);
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role {RoleName}", roleName);
                return new List<UserDto>();
            }
        }

        public async Task<List<UserDto>> GetUsersByDepartmentAsync(Guid departmentId)
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
                    return response.Message.Users;
                }

                _logger.LogWarning("Failed to get users by department {DepartmentId}: {Error}",
                    departmentId, response.Message.ErrorMessage);
                return new List<UserDto>();
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting users by department {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by department {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
        }
    }
}
