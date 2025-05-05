using System.Reflection.Metadata.Ecma335;
using Auth.API.Constants;
using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using AutoMapper.Features;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
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
            _logger.LogError($"Login failed with {request.Username}");
            return Problem(MessageConstant.User.LoginFailed);
        }
        _logger.LogInformation($"Login succeeded with {request.Username}");
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
            _logger.LogError($"Register failed with {request.Username}");
            return Problem(MessageConstant.User.RegisterFail);
        }
        _logger.LogInformation($"Register successful with {request.Username}");
        return CreatedAtAction(nameof(Register), response);
    }
    
    [HttpPost(ApiEndPointConstant.User.SendOtp)]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendOtp([FromBody] GenerateEmailOtpRequest request)
    {
        var result = await _userService.GenerateOtpAsync(request);
        if (result == null)
        {
            return Problem(MessageConstant.OTP.SendOtpFailed);
        }
    
        return CreatedAtAction(nameof(SendOtp), result);
    }
    
}