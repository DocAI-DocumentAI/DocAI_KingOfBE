using Auth.API.Services.Interface;
using MassTransit;
using Shared.DTOs;

namespace Auth.API.Consumers;

/// <summary>
/// Consumer for handling department employee requests from Document service
/// </summary>
public class DepartmentEmployeeConsumer : IConsumer<DepartmentEmployeeRequest>
{
    private readonly IUserService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<DepartmentEmployeeConsumer> _logger;

    public DepartmentEmployeeConsumer(
        IUserService userService,
        IDepartmentService departmentService,
        ILogger<DepartmentEmployeeConsumer> logger)
    {
        _userService = userService;
        _departmentService = departmentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DepartmentEmployeeRequest> context)
    {
        var request = context.Message;
        
        try
        {
            _logger.LogInformation("Processing department employee request {RequestId} for department {DepartmentId}, ManagersOnly: {ManagersOnly}",
                request.RequestId, request.DepartmentId, request.ManagersOnly);

            var response = new DepartmentEmployeeResponse
            {
                RequestId = request.RequestId,
                Success = true
            };

            // Get employee emails based on whether managers only or all employees
            List<string> employeeEmails;
            if (request.ManagersOnly)
            {
                employeeEmails = await _userService.GetDepartmentManagerEmailsAsync(request.DepartmentId);
            }
            else
            {
                employeeEmails = await _userService.GetDepartmentEmployeeEmailsAsync(request.DepartmentId);
            }

            response.EmployeeEmails = employeeEmails;

            _logger.LogInformation("Successfully retrieved {Count} employee emails for department {DepartmentId}",
                employeeEmails.Count, request.DepartmentId);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing department employee request {RequestId} for department {DepartmentId}",
                request.RequestId, request.DepartmentId);

            var errorResponse = new DepartmentEmployeeResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = ex.Message
            };

            await context.RespondAsync(errorResponse);
        }
    }
}
