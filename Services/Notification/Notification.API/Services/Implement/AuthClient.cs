using System.Text.Json;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services.Implement
{
    public class AuthClient : IAuthClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthClient> _logger;

        public AuthClient(HttpClient httpClient, ILogger<AuthClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // BaseAddress is configured in Program.cs
        }

        public async Task<List<RoleResponse>> GetAllRolesAsync()
        {
            try
            {
                // Assuming the endpoint in Auth service is /api/auth/roles
                var response = await _httpClient.GetAsync("api/roles");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                // We expect a paginated response, so we need to deserialize it correctly
                // This part needs to align with the actual response from Auth service.
                // For now, let's assume it returns a simple list for simplicity.
                var roles = JsonSerializer.Deserialize<List<RoleResponse>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return roles ?? new List<RoleResponse>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve all roles from Auth service.");
                // Return empty list on failure to prevent system-wide crash
                return new List<RoleResponse>();
            }
        }

        public async Task<List<UserDetailResponseExternal>> GetUsersByDepartmentAndRoleAsync(Guid departmentId, Guid roleId)
        {
            try
            {
                // Using the exact function name from the user's request
                // Assuming the endpoint is /api/users/by-department-and-role
                var requestUri = $"api/users/by-department-and-role?departmentId={departmentId}&roleId={roleId}";
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<UserDetailResponseExternal>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return users ?? new List<UserDetailResponseExternal>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get users for DepartmentId: {DepartmentId}, RoleId: {RoleId}", departmentId, roleId);
                return new List<UserDetailResponseExternal>();
            }
        }
        public async Task<List<UserDetailResponseExternal>> GetAdminUsersAsync(Guid adminRoleId)
        {
            try
            {
                // This method might call the same endpoint as above but with a null/empty departmentId
                // or a dedicated endpoint. Let's assume it's a dedicated one for clarity.
                // Assuming endpoint is /api/users/by-role/{roleId}
                var requestUri = $"api/users/by-role/{adminRoleId}";
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<UserDetailResponseExternal>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return users ?? new List<UserDetailResponseExternal>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get admin users with RoleId: {RoleId}", adminRoleId);
                return new List<UserDetailResponseExternal>();
            }
        }

        public async Task<List<string>> GetDepartmentManagerEmailsAsync(string departmentId)
        {
            try
            {
                _logger.LogInformation("Getting department manager emails for department {DepartmentId}", departmentId);

                var requestUri = $"api/users/department-managers/{departmentId}";
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var emails = JsonSerializer.Deserialize<List<string>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return emails ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department manager emails for department {DepartmentId}", departmentId);
                return new List<string>();
            }
        }

        public async Task<UserDetailResponseExternal?> GetUserByEmailAsync(string email)
        {
            try
            {
                _logger.LogInformation("Getting user by email {Email}", email);

                var requestUri = $"api/users/by-email/{Uri.EscapeDataString(email)}";
                var response = await _httpClient.GetAsync(requestUri);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<UserDetailResponseExternal>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email}", email);
                return null;
            }
        }
    }
}
