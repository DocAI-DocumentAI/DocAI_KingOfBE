using Auth.API.Services.Interface;
using MassTransit;
using Shared.DTOs;

namespace Auth.API.Consumers;

/// <summary>
/// Consumer for handling user email requests from Document service
/// </summary>
public class UserEmailConsumer : IConsumer<UserEmailRequest>
{
    private readonly IUserService _userService;
    private readonly ILogger<UserEmailConsumer> _logger;

    public UserEmailConsumer(
        IUserService userService,
        ILogger<UserEmailConsumer> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserEmailRequest> context)
    {
        var request = context.Message;
        
        try
        {
            _logger.LogInformation("Processing user email request {RequestId} for user {UserId}",
                request.RequestId, request.UserId);

            var response = new UserEmailResponse
            {
                RequestId = request.RequestId,
                Success = true
            };

            // Get user email by ID
            var userEmail = await _userService.GetUserEmailByIdAsync(request.UserId);
            
            if (!string.IsNullOrEmpty(userEmail))
            {
                response.Email = userEmail;
                _logger.LogInformation("Successfully retrieved email for user {UserId}", request.UserId);
            }
            else
            {
                response.Success = false;
                response.ErrorMessage = $"User with ID {request.UserId} not found";
                _logger.LogWarning("User {UserId} not found", request.UserId);
            }

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user email request {RequestId} for user {UserId}",
                request.RequestId, request.UserId);

            var errorResponse = new UserEmailResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await context.RespondAsync(errorResponse);
        }
    }
}
