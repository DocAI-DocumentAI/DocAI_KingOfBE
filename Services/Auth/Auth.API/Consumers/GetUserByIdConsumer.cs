using Auth.API.Services.Interface;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Auth.API.Consumers
{
    public class GetUserByIdConsumer : IConsumer<GetUserByIdCommand>
    {
        private readonly IUserService _userService;
        private readonly ILogger<GetUserByIdConsumer> _logger;

        public GetUserByIdConsumer(IUserService userService, ILogger<GetUserByIdConsumer> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GetUserByIdCommand> context)
        {
            try
            {
                var command = context.Message;
                _logger.LogInformation("Getting user by ID {UserId}", command.UserId);

                var user = await _userService.GetUserByIdAminAsync(command.UserId);

                if (user != null)
                {
                    await context.RespondAsync(new GetUserByIdResponse
                    {
                        User = new UserDto
                        {
                            UserId = user.Id,
                            Email = user.Email ?? "",
                            Name = user.FullName ?? "",
                            DepartmentName = user.Department?.Name ?? "",
                            Role = user.Role?.RoleName ?? "",
                            DepartmentId = user.Department?.Id
                        },
                        Success = true,
                        RequestId = command.RequestId
                    });
                }
                else
                {
                    await context.RespondAsync(new GetUserByIdResponse
                    {
                        User = null,
                        Success = false,
                        ErrorMessage = "User not found",
                        RequestId = command.RequestId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID");
                await context.RespondAsync(new GetUserByIdResponse
                {
                    User = null,
                    Success = false,
                    ErrorMessage = ex.Message,
                    RequestId = context.Message.RequestId
                });
            }
        }
    }
}
