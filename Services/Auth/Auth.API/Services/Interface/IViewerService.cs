using Auth.API.Payload.Request;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface;

public interface IViewerService
{
    public Task<ViewerResponse> GetInformationOfViewerAsync();
    public Task<IPaginate<ViewerResponse>> GetAllViewersAsync(int page, int size, ViewerFilter? filter, string? sortBy,
        bool isAsc);
    public Task<ViewerResponse> UpdateViewerAsync(UpdateViewerRequest updateViewerRequest);
    public Task<ViewerResponse> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest);
}