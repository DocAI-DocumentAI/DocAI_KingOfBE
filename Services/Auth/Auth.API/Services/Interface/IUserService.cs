using System.Threading.Tasks;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.ActiveKey;
using Auth.API.Payload.Response.User;
using MassTransit;
using Microsoft.AspNetCore.Identity.Data;
using Shared.DTOs;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Services.Interface;

public interface IUserService
{
    public Task<LoginResponse> LoginAsync(LoginRequest request);

    public Task<RegisterResponse> RegisterAsync(RegisterRequest request);

    public Task<string> GenerateOtpAsync(GenerateEmailOtpRequest request);

    public Task<UserRoleChangeResponse> ChangeUserRoleAsync(string activationCode);

    public Task<ChangeDepartmentResponse> ChangeDepartmentForUserAsync(ChangeDepartmentRequest request);

}
