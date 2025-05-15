using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface;

public interface IStaffService
{
    public Task<IPaginate<StaffResponse>> GetAllStaffsAsync(int page, int size, StaffFilter? filter, string? sortby, bool isAsc);
    public Task<StaffResponse> GetStaffInformationAsync();
    public Task<StaffResponse> UpdateStaffAsync(UpdateStaffRequest request);
}