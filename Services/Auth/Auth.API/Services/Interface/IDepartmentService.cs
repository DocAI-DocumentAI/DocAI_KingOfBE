using Auth.API.Payload.Request.Department;
using Auth.API.Payload.Response;
using Auth.API.Payload.Response.Department;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;

namespace Auth.API.Services.Interface;

public interface IDepartmentService
{
    public Task<IPaginate<DepartmentResponse>> GetAllDepartmentsAsync(int page, int size, DepartmentFilter? filter, string? sortby, bool isAsc);
    public Task<DepartmentResponse> GetDepartmentInformationAsync(Guid DepartmentId);
    public Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request);
    public Task<DepartmentResponse> UpdateDepartmentAsync(UpdateDepartmentRequest request, Guid DepartmentId);
    public Task<DepartmentResponse> DeleteDepartmentAsync(Guid DepartmentId);
    
    /// <summary>
    /// Get department names by their IDs for bulk lookup
    /// </summary>
    /// <param name="departmentIds">List of department IDs</param>
    /// <returns>Dictionary mapping department IDs to department names</returns>
    public Task<Dictionary<string, string>> GetDepartmentNamesByIdsAsync(List<string> departmentIds);
}