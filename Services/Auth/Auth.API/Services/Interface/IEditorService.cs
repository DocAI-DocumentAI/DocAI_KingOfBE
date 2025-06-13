// using Auth.API.Payload.Request.Staff;
// using Auth.API.Payload.Response.Staff;
// using Auth.Infrastructure.Filter;
// using Auth.Infrastructure.Paginate;
//
// namespace Auth.API.Services.Interface;
//
// public interface IEditorService
// {
//     public Task<IPaginate<EditorResponse>> GetAllEditorsAsync(int page, int size, EditorFilter? filter, string? sortby, bool isAsc);
//     public Task<EditorResponse> GetEditorInformationAsync();
//     public Task<EditorResponse> UpdateEditorAsync(UpdateEditorRequest request);
// }