using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class TagController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }

        [HttpPost(ApiEndPointConstant.Tag.CreateTag)]
        [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request, string userId)
        {
            var response = await _tagService.CreateTagAsync(request, userId);
            return Ok(ApiResponse<object>.Success(response, "Tag created successfully.", 201));
        }

        [HttpGet(ApiEndPointConstant.Tag.GetTagById)]
        [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTagById([FromRoute(Name = "id")] string tagId)
        {
            var response = await _tagService.GetTagByIdAsync(tagId);
            return Ok(ApiResponse<object>.Success(response));
        }

        [HttpGet(ApiEndPointConstant.Tag.GetAllTags)]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<TagResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllTags(int pageNumber = 1, int pageSize = 10)
        {
            var response = await _tagService.GetAllTagsAsync(pageNumber, pageSize);
            return Ok(ApiResponse<object>.Success(response));
        }

        [HttpPut(ApiEndPointConstant.Tag.UpdateTag)]
        [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTag([FromRoute(Name = "id")] string tagId, [FromBody] UpdateTagRequest request, string userId)
        {
            var response = await _tagService.UpdateTagAsync(tagId, request, userId);
            return Ok(ApiResponse<object>.Success(response, "Tag updated successfully."));
        }

        [HttpDelete(ApiEndPointConstant.Tag.DeleteTag)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTag([FromRoute(Name = "id")] string tagId)
        {
            await _tagService.DeleteTagAsync(tagId);
            return Ok(ApiResponse<object>.Success(null, "Tag deleted successfully."));
        }
    }
}
