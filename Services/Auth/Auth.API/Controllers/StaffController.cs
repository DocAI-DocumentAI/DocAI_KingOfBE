using Auth.API.Constants;
using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.API.Services.Interface;
using Auth.API.Validators;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;
[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class StaffController : ControllerBase
{
    private IStaffService _staffService;
    private readonly ILogger<StaffController> _logger;

    public StaffController(IStaffService staffService, ILogger<StaffController> logger)
    {
        _staffService = staffService;
        _logger = logger;
    }

    [HttpGet(ApiEndPointConstant.Staff.Staffs)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Manager)]
    public async Task<IActionResult> GetAllStaffsAsync(int page = 1, int size = 30,[FromQuery] StaffFilter? filter = null, string? sortBy =null, bool isAsc = true)
    {
        var response = await _staffService.GetAllStaffsAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Staff.StaffInformation)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Manager, RoleEnum.Staff)]
    public async Task<IActionResult> GetStaffInformationAsync()
    {
        var response = await _staffService.GetStaffInformationAsync();
        return Ok(response);
    }
    [HttpPatch(ApiEndPointConstant.Staff.UpdateStaff)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StaffResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Manager, RoleEnum.Staff)]
    public async Task<IActionResult> UpdateStaffAsync(UpdateStaffRequest updateStaffRequest)
    {
        var response = await _staffService.UpdateStaffAsync(updateStaffRequest);
        if (response == null)
        {
            _logger.LogError($"Update member failed");
            return Problem(MessageConstant.Staff.UpdateFail);
        }
        _logger.LogInformation($"Update member successful");
        return Ok(response);
    }
}