using System.Threading.Tasks;
using Auth.API.Payload.Request;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Request.Auth;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Auth;
using Auth.API.Payload.Response.User;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using MassTransit;
using Microsoft.AspNetCore.Identity.Data;
using Shared.DTOs;
using LoginRequest = Auth.API.Payload.Request.LoginRequest;
using RegisterRequest = Auth.API.Payload.Request.RegisterRequest;

namespace Auth.API.Services.Interface;

public interface IUserService
{
    public Task<LoginResponse> LoginAsync(LoginRequest request);
    public Task<RegisterResponse> CreateUserAsync(RegisterRequest request);
    public Task<string> GenerateOtpAsync(GenerateEmailOtpRequest request);
    public Task<UserRoleChangeResponse> ChangeUserRoleAsync(Guid roleId);
    public Task<ChangeDepartmentResponse> ChangeDepartmentForUserAsync(ChangeDepartmentRequest request);
    public Task<List<GetUserByDeparAndRoleResponse>> GetUserByDeparAndRoleAsync(GetUserByDeparAndRole request);
    public Task<IPaginate<UserResponse>> GetAllUsersAsync(int page, int size, UserFilter? filter, string? sortBy, bool isAsc);
    public Task<bool> LogoutAsync();
    public Task<Dictionary<string, string>> GetUserNamesByIdsAsync(List<string> userIds);
    public Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request);
    public Task<GoogleOAuthResponse> GoogleLoginAsync(GoogleLoginRequest request);
    public Task<GoogleOAuthResponse> GoogleCallbackAsync(string code, string state);
    public Task<bool> RevokeGoogleTokenAsync(string userId);
    public Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}
