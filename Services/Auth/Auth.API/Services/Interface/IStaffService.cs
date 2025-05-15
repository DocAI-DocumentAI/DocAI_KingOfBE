using Auth.API.Filter;
using Auth.API.Models;
using Auth.API.Paginate;
using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;

namespace Auth.API.Services.Interface;

public interface IStaffService
{
    public Task<IPaginate<StaffResponse>> GetAllStaffsAsync(int page, int size, StaffFilter? filter, string? sortby, bool isAsc);
    public Task<StaffResponse> GetStaffInformationAsync();
    public Task<StaffResponse> UpdateStaffAsync(UpdateStaffRequest request);
}