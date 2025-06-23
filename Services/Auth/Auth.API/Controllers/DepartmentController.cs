using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Department;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class DepartmentController : ControllerBase
{
    private IDepartmentService _DepartmentService;
    private readonly ILogger<DepartmentController> _logger;

    public DepartmentController(IDepartmentService DepartmentService, ILogger<DepartmentController> logger)
    {
        _DepartmentService = DepartmentService;
        _logger = logger;
    }

    [HttpGet(ApiEndPointConstant.Department.Departments)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllDepartmentsAsync(int page = 1, int size = 30,
        [FromQuery] DepartmentFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        var response = await _DepartmentService.GetAllDepartmentsAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Department.DepartmentInformation + "/{departmentId}")]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEditorInformationAsync(Guid departmentId)
    {
        var response = await _DepartmentService.GetDepartmentInformationAsync(departmentId);
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.Department.CreateDepartment)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDepartmentAsync([FromBody] CreateDepartmentRequest request)
    {
        var response = await _DepartmentService.CreateDepartmentAsync(request);
        if (response == null)
        {
            _logger.LogError("Create Department Failed");
            return Problem(MessageConstant.Department.CreateFailed);
        }

        _logger.LogInformation("Create Department Success");
        return Created($"{ApiEndPointConstant.Department.DepartmentInformation}/{response.Id}", response);
    }

    [HttpPatch(ApiEndPointConstant.Department.UpdateDepartment)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEditorAsync([FromBody] UpdateDepartmentRequest updateDepartmentRequest, Guid departmentId)
    {
        var response = await _DepartmentService.UpdateDepartmentAsync(updateDepartmentRequest, departmentId);
        if (response == null)
        {
            _logger.LogError($"Update Department failed");
            return Problem(MessageConstant.Department.UpdateFailed);
        }

        _logger.LogInformation($"Update Department successful");
        return Ok(response);
    }

    [HttpDelete(ApiEndPointConstant.Department.DeleteDepartment)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DepartmentResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDepartmentAsync(Guid departmentId)
    {
        var response = await _DepartmentService.DeleteDepartmentAsync(departmentId);
        if (response == null)
        {
            _logger.LogError($"Delete Department failed");
            return Problem(MessageConstant.Department.DeleteFailed);
        }

        _logger.LogInformation($"Delete Department successful");
        return Ok(response);
    }
}
