using System.Threading.Tasks;
using Auth.API.Payload.Request;
using Auth.API.Payload.Response;
using Microsoft.AspNetCore.Identity.Data;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Services.Interface;

public interface IUserService
{
    public Task<MemberResponse> GetInformationOfMemberAsync();
    
    public Task<LoginResponse> LoginAsync(LoginRequest request);
    
    public Task<RegisterResponse> RegisterAsync(RegisterRequest request);

    public Task<string> GenerateOtpAsync(GenerateEmailOtpRequest request);
}