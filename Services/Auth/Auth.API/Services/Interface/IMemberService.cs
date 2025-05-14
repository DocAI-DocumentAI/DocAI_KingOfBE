using Auth.API.Filter;
using Auth.API.Paginate;
using Auth.API.Payload.Response;

namespace Auth.API.Services.Interface;

public interface IMemberService
{
    public Task<MemberResponse> GetInformationOfMemberAsync();
    public Task<IPaginate<MemberResponse>> GetAllMemberAsync(int page, int size, MemberFilter? filter, string? sortBy,
        bool isAsc);
}