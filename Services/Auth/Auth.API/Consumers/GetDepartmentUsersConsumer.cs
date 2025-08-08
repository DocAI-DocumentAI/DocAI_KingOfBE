using Auth.API.Services.Interface;
using Auth.Infrastructure.Filter;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetDepartmentUsersConsumer : IConsumer<GetDepartmentUsersCommand>
    {
        private readonly IUserService _userService;
        private readonly ILogger<GetDepartmentUsersConsumer> _logger;

        public GetDepartmentUsersConsumer(IUserService userService, ILogger<GetDepartmentUsersConsumer> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetDepartmentUsersCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting users for department {DepartmentId} with role filter {RoleFilter}",
                    command.DepartmentId, command.RoleFilter);

                var users = await GetDepartmentUsersAsync(command.DepartmentId, command.RoleFilter);

                await context.RespondAsync(new GetDepartmentUsersResponse
                {
                    Users = users,
                    Success = true,
                    RequestId = command.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department users");
                await context.RespondAsync(new GetDepartmentUsersResponse
                {
                    Users = new List<UserDto>(),
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }

        private async Task<List<UserDto>> GetDepartmentUsersAsync(Guid departmentId, string? roleFilter)
        {
            try
            {
                // Get all users in department
                var allUsers = await _userService.GetAllUsersAsync(1, 1000, new UserFilter
                {
                    DepartmentId = departmentId
                }, "FullName", true);

                var users = allUsers.Items.AsEnumerable();

                // Filter by role if specified
                if (!string.IsNullOrEmpty(roleFilter))
                {
                    users = users.Where(u => u.Role.RoleName.Equals(roleFilter, StringComparison.OrdinalIgnoreCase));
                }

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
                _logger.LogError(ex, "Error fetching department users for {DepartmentId}", departmentId);
                return new List<UserDto>();
            }
        }
    }
}
