using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.User;
using Auth.API.Services.Interface;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using AutoMapper.Features;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class AuthController : ControllerBase
{
    private IUserService _userService;
    readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger, IUserService userService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    [HttpPost(ApiEndPointConstant.User.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _userService.LoginAsync(request);
        if (response == null)
        {
            _logger.LogError($"Login failed with {request.Email}");
            return Problem(MessageConstant.User.LoginFailed);
        }
        _logger.LogInformation($"Login succeeded with {request.Email}");
        return Ok(response);
    }

    [HttpPost(ApiEndPointConstant.User.Register)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _userService.RegisterAsync(request);
        if (response == null)
        {
            _logger.LogError($"Register failed with {request.Email}");
            return Problem(MessageConstant.User.RegisterFail);
        }
        _logger.LogInformation($"Register successful with {request.Email}");
        return CreatedAtAction(nameof(Register), response);
    }



    [HttpPost(ApiEndPointConstant.User.SendOtp)]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    // [Authorize(Roles = $"{nameof(RoleEnum.Admin)},{nameof(RoleEnum.Manager)}")]
    public async Task<IActionResult> SendOtp([FromBody] GenerateEmailOtpRequest request)
    {
        var result = await _userService.GenerateOtpAsync(request);
        if (result == null)
        {
            return Problem(MessageConstant.OTP.SendOtpFailed);
        }

        return CreatedAtAction(nameof(SendOtp), result);
    }

    [HttpPost(ApiEndPointConstant.User.ChangeRole)]
    [Authorize]
    [ProducesResponseType(typeof(UserRoleChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangeUserRole(Guid roleId)
    {
        try
        {
            var result = await _userService.ChangeUserRoleAsync(roleId);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to change user role: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError($"Unauthorized access when changing role: {ex.Message}");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error changing user role: {ex.Message}");
            return Problem(ex.Message);
        }
    }

    [HttpPost(ApiEndPointConstant.User.ChangeDepartment)]
    [Authorize]
    [ProducesResponseType(typeof(ChangeDepartmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangeDepartmentForUser([FromBody] ChangeDepartmentRequest request)
    {
        try
        {
            var result = await _userService.ChangeDepartmentForUserAsync(request);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to change user department: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError($"Unauthorized access when changing department: {ex.Message}");
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error changing user department: {ex.Message}");
            return Problem(ex.Message);
        }
    }

    [HttpPost(ApiEndPointConstant.User.GetUsersByDepartmentAndRole)]
    [Authorize]
    [ProducesResponseType(typeof(List<GetUserByDeparAndRoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUsersByDepartmentAndRole([FromBody] GetUserByDeparAndRole request)
    {
        try
        {
            var result = await _userService.GetUserByDeparAndRoleAsync(request);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to get users by department and role: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting users by department and role: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet(ApiEndPointConstant.User.Users)]
    [ProducesResponseType(typeof(IPaginate<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllUsersAsync(int page = 1, int size = 30,
        [FromQuery] UserFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        try
        {
            var response = await _userService.GetAllUsersAsync(page, size, filter, sortBy, isAsc);
            return Ok(response);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogError($"Failed to get users: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting users: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
