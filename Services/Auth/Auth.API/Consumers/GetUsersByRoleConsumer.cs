using Auth.API.Services.Interface;
using Auth.Infrastructure.Filter;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetUsersByRoleConsumer : IConsumer<GetUsersByRoleCommand>
    {
        private readonly IUserService _userService;
        private readonly ILogger<GetUsersByRoleConsumer> _logger;

        public GetUsersByRoleConsumer(IUserService userService, ILogger<GetUsersByRoleConsumer> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetUsersByRoleCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting users for role {RoleName}", command.RoleName);

                var users = await GetUsersByRoleAsync(command.RoleName);

                await context.RespondAsync(new GetUsersByRoleResponse
                {
                    Users = users,
                    Success = true,
                    RequestId = command.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role");
                await context.RespondAsync(new GetUsersByRoleResponse
                {
                    Users = new List<UserDto>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }

        private async Task<List<UserDto>> GetUsersByRoleAsync(string roleName)
        {
            try
            {
                var allUsers = await _userService.GetAllUsersAsync(1, 1000, new UserFilter
                {
                    // Add role filter if available in UserFilter
                }, "FullName", true);

                var users = allUsers.Items
                    .Where(u => u.Role?.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase) == true);

                return users.Select(u => new UserDto
                {
                    UserId = u.Id,
                    Email = u.Email ?? "",
                    Name = u.FullName ?? "",
                    DepartmentName = u.Department?.Name ?? "",
                    Role = u.Role?.RoleName ?? ""
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users by role {RoleName}", roleName);
                return new List<UserDto>();
            }
        }
    }
}
