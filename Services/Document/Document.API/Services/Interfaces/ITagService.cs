using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Document.API.Services.Interfaces
{
    public interface ITagService
    {
        Task<TagResponse> CreateTagAsync(CreateTagRequest request);
        Task<TagResponse> GetTagByIdAsync(string tagId);
        Task<IPaginate<TagResponse>> GetAllTagsAsync(int pageNumber, int pageSize);
        Task<TagResponse> UpdateTagAsync(string tagId, UpdateTagRequest request);
        Task DeleteTagAsync(string tagId);
    }
}
