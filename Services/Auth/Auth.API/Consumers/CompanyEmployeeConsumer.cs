using Auth.API.Services.Interface;
using MassTransit;
using Shared.DTOs;

namespace Auth.API.Consumers;

/// <summary>
/// Consumer for handling company-wide employee requests from Document service
/// </summary>
public class CompanyEmployeeConsumer : IConsumer<CompanyEmployeeRequest>
{
    private readonly IUserService _userService;
    private readonly ILogger<CompanyEmployeeConsumer> _logger;

    public CompanyEmployeeConsumer(
        IUserService userService,
        ILogger<CompanyEmployeeConsumer> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CompanyEmployeeRequest> context)
    {
        var request = context.Message;
        
        try
        {
            _logger.LogInformation("Processing company employee request {RequestId}", request.RequestId);

            var response = new CompanyEmployeeResponse
            {
                RequestId = request.RequestId,
                Success = true
            };

            // Get all company employee emails
            var employeeEmails = await _userService.GetAllCompanyEmployeeEmailsAsync();
            response.EmployeeEmails = employeeEmails;

            _logger.LogInformation("Successfully retrieved {Count} company employee emails", employeeEmails.Count);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing company employee request {RequestId}", request.RequestId);

            var errorResponse = new CompanyEmployeeResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await context.RespondAsync(errorResponse);
        }
    }
}
