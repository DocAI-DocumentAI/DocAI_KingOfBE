using Auth.API.Filter;
using Auth.API.Paginate;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;

namespace Auth.API.Services.Interface;

public interface IMemberService
{
    public Task<MemberResponse> GetInformationOfMemberAsync();
    public Task<IPaginate<MemberResponse>> GetAllMembersAsync(int page, int size, MemberFilter? filter, string? sortBy,
        bool isAsc);
    public Task<MemberResponse> UpdateMemberAsync(UpdateMemberRequest updateMemberRequest);
}