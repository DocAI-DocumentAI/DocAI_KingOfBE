using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Response.ActiveKey;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface
{
    public interface IActiveKeyService
    {
        Task<ActiveKeyResponse> CreateActiveKeyAsync(ActiveKeyRequest request);
        Task<string> GenerateActivationCode(int length = 32);
        Task<IPaginate<ActiveKeyListResponse>> GetAllActiveKeysAsync(int page, int size, ActiveKeyFilter? filter, string? sortBy, bool isAsc);
        Task<ActiveKeyListResponse> GetActiveKeyByIdAsync(Guid id);
        Task<ActiveKeyListResponse> UpdateActiveKeyAsync(Guid id, UpdateActiveKeyRequest request);
        Task<ActiveKeyListResponse> DeleteActiveKeyAsync(Guid id);
    }
}
