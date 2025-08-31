using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ISearchService searchService, ILogger<SearchController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        /// <summary>
        /// Perform a natural language search using Kernel Memory to get AI-powered answers with source citations.
        /// </summary>
        /// <param name="request">Search request with natural language query and filters.</param>
        /// <returns>An ApiResponse containing the search results, including an AI-generated answer and relevant document sources.</returns>
        [HttpGet(ApiEndPointConstant.Document.KernelMemorySearch)]
        [ProducesResponseType(typeof(ApiResponse<EnhancedSemanticSearchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> KernelMemorySearch([FromQuery] SemanticSearchRequest request)
        {
            try
            {
                var filter = new KernelMemorySearchFilter
                {
                    DepartmentId = request.DepartmentId,
                    DocumentTypeId = request.DocumentTypeId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    EffectiveFrom = request.EffectiveFrom,
                    EffectiveUntil = request.EffectiveUntil
                };

                var result = await _searchService.SearchWithKernelMemoryAsync(request, filter);

                if (result.Success)
                {
                    return Ok(ApiResponse<EnhancedSemanticSearchResponse>.Success(result, "Search completed successfully."));
                }
                else
                {
                    return BadRequest(ApiResponse<object>.Error("SEARCH_FAILED", result.ErrorMessage));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search request: {Message}", ex.Message);
                return BadRequest(ApiResponse<object>.Error("INVALID_REQUEST", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during Kernel Memory search.");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Error("SEARCH_ERROR", "An unexpected error occurred."));
            }
        }
    }
}
