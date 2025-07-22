using Auth.API.Services.Interface;
using MassTransit;
using Shared.DTOs;

namespace Auth.API.Consumers;

/// <summary>
/// Consumer for handling name lookup requests from other services
/// </summary>
public class NameLookupConsumer : IConsumer<NameLookupRequest>
{
    private readonly IUserService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<NameLookupConsumer> _logger;

    public NameLookupConsumer(
        IUserService userService,
        IDepartmentService departmentService,
        ILogger<NameLookupConsumer> logger)
    {
        _userService = userService;
        _departmentService = departmentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NameLookupRequest> context)
    {
        _logger.LogInformation("NameLookupConsumer: Received request {RequestId}", context.Message.RequestId);
        
        try
        {
            _logger.LogInformation("Processing name lookup request {RequestId} for {UserCount} users and {DeptCount} departments",
                context.Message.RequestId, context.Message.UserIds.Count, context.Message.DepartmentIds.Count);

            var response = new NameLookupResponse
            {
                RequestId = context.Message.RequestId,
                Success = true
            };

            // Lookup user names
            if (context.Message.UserIds.Any())
            {
                var userNames = await _userService.GetUserNamesByIdsAsync(context.Message.UserIds);
                response.UserNames = userNames;
            }

            // Lookup department names
            if (context.Message.DepartmentIds.Any())
            {
                var departmentNames = await _departmentService.GetDepartmentNamesByIdsAsync(context.Message.DepartmentIds);
                response.DepartmentNames = departmentNames;
            }

            await context.RespondAsync(response);
            
            _logger.LogInformation("Successfully processed name lookup request {RequestId}", context.Message.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing name lookup request {RequestId}", context.Message.RequestId);
            
            await context.RespondAsync(new NameLookupResponse
            {
                RequestId = context.Message.RequestId,
                Success = false,
                ErrorMessage = "Failed to lookup names"
            });
        }
    }
}
