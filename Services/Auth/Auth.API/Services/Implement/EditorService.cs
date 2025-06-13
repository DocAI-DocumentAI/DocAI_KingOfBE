// using System.Security.Authentication;
// using Auth.API.Constants;
// using Auth.API.Payload.Request.Staff;
// using Auth.API.Payload.Response.Staff;
// using Auth.API.Services.Interface;
// using Auth.Domain.Models;
// using Auth.Infrastructure.Filter;
// using Auth.Infrastructure.Paginate;
// using Auth.Infrastructure.Repository.Interfaces;
// using AutoMapper;
// using Microsoft.EntityFrameworkCore;
//
// namespace Auth.API.Services.Implement;
//
// public class EditorService : BaseService<Role> , IEditorService
// {
//     private readonly IConfiguration _configuration;
//     public EditorService(IUnitOfWork<DocAIAuthContext> unitOfWork, ILogger<Role> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : base(unitOfWork, logger, mapper, httpContextAccessor, configuration)
//     {
//         _configuration = configuration;
//     }
//
//     public async Task<IPaginate<EditorResponse>> GetAllEditorsAsync(int page, int size, EditorFilter? filter, string? sortBy, bool isAsc)
//     {
//         var Viewers = await _unitOfWork.GetRepository<Role>().GetPagingListAsync(
//             selector: s => new Role()
//             {
//                 Id = s.Id,
//                 UserId = s.UserId,
//                 User = s.User,
//                 Type = s.Type,
//                 CreateAt = s.CreateAt,
//                 UpdateAt = s.UpdateAt,
//             },
//             page: page,
//             size: size,
//             filter: filter,
//             sortBy: sortBy,
//             isAsc: isAsc,
//             include: s => s.Include(s => s.User)
//             );
//         var response = _mapper.Map<IPaginate<EditorResponse>>(Viewers);
//         return response;
//     }
//
//     public async Task<EditorResponse> GetEditorInformationAsync()
//     {
//         var userId = GetUserIdFromJwt();
//         if (userId == null)
//             throw new AuthenticationException(MessageConstant.User.UserNotFound);
//         var Viewer = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
//             predicate: s => s.UserId == userId,
//             include: s => s.Include(s => s.User)
//             );
//         if (Viewer == null)
//             throw new BadHttpRequestException(MessageConstant.Viewer.ViewerNotFound);
//         var response = _mapper.Map<EditorResponse>(Viewer);
//         return response;
//     }
//
//     public async Task<EditorResponse> UpdateEditorAsync(UpdateEditorRequest request)
//     {
//         var userId = GetUserIdFromJwt();
//         if (userId == null)
//             throw new AuthenticationException(MessageConstant.User.UserNotFound);
//         var Viewer = await _unitOfWork.GetRepository<Role>().SingleOrDefaultAsync(
//             predicate: s => s.UserId == userId,
//             include: s => s.Include(s => s.User)
//             );
//         Viewer.User.Email = string.IsNullOrEmpty(request.Email) ? Viewer.User.Email : request.Email;
//         Viewer.User.FullName = string.IsNullOrEmpty(request.FullName) ? Viewer.User.FullName : request.FullName;
//         Viewer.User.Phone = string.IsNullOrEmpty(request.Phone) ? Viewer.User.Phone : request.Phone;
//         Viewer.User.TwoFactorEnabled = request.TwoFactorEnabled.HasValue ? request.TwoFactorEnabled.Value : Viewer.User.TwoFactorEnabled;
//         Viewer.User.TwoFactorMethod = string.IsNullOrEmpty(request.TwoFactorMethod) ? Viewer.User.TwoFactorMethod : request.TwoFactorMethod;
//         Viewer.Type = string.IsNullOrEmpty(request.Type) ? Viewer.Type : request.Type;
//         Viewer.UpdateAt = DateTime.UtcNow;
//         Viewer.User.UpdateAt = DateTime.UtcNow;
//         _unitOfWork.GetRepository<User>().UpdateAsync(Viewer.User);
//         _unitOfWork.GetRepository<Role>().UpdateAsync(Viewer);
//         var isSuccess = await _unitOfWork.CommitAsync() > 0;
//         EditorResponse response = null;
//         if (isSuccess) response = _mapper.Map<EditorResponse>(Viewer);
//         return response;
//     }
// }