using Auth.API.Services.Interface;
using MassTransit;
using Shared.DTOs;

namespace Auth.API.Consumers;

/// <summary>
/// Consumer for handling get all departments requests from Document service
/// </summary>
public class GetAllDepartmentsConsumer : IConsumer<GetAllDepartmentsRequest>
{
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<GetAllDepartmentsConsumer> _logger;

    public GetAllDepartmentsConsumer(
        IDepartmentService departmentService,
        ILogger<GetAllDepartmentsConsumer> logger)
    {
        _departmentService = departmentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetAllDepartmentsRequest> context)
    {
        var request = context.Message;
        
        try
        {
            _logger.LogInformation("Processing get all departments request {RequestId}", request.RequestId);

            var response = new GetAllDepartmentsResponse
            {
                RequestId = request.RequestId,
                Success = true
            };

            // Get all departments (using a large page size to get all)
            var departmentsResult = await _departmentService.GetAllDepartmentsAsync(1, 1000, null, "Name", true);
            
            if (departmentsResult?.Items != null)
            {
                response.Departments = departmentsResult.Items.Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description
                }).ToList();

                _logger.LogInformation("Retrieved {Count} departments for request {RequestId}", 
                    response.Departments.Count, request.RequestId);
            }
            else
            {
                _logger.LogWarning("No departments found for request {RequestId}", request.RequestId);
            }

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing get all departments request {RequestId}", request.RequestId);
            
            await context.RespondAsync(new GetAllDepartmentsResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorMessage = ex.Message,
                Departments = new List<DepartmentDto>()
            });
        }
    }
}
