using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Models;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class DocumentReplacementController : ControllerBase
    {
        private readonly IDocumentReplacementService _replacementService;
        private readonly ILogger<DocumentReplacementController> _logger;

        public DocumentReplacementController(
            IDocumentReplacementService replacementService,
            ILogger<DocumentReplacementController> logger)
        {
            _replacementService = replacementService;
            _logger = logger;
        }

        [HttpPost(ApiEndPointConstant.Document.GetReplacementSuggestions)]
        [ProducesResponseType(typeof(ApiResponse<DocumentReplacementSuggestionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReplacementSuggestions(
            [FromBody] DocumentReplacementSuggestionRequest request)
        {
            try
            {
                _logger.LogInformation("Getting replacement suggestions for document type {DocumentTypeId}",
                    request.DocumentTypeId);

                var result = await _replacementService.GetReplacementSuggestionsAsync(request);

                return Ok(ApiResponse<DocumentReplacementSuggestionResponse>.Success(
                    result,
                    $"Found {result.TotalFound} replacement suggestions",
                    StatusCodes.Status200OK));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting replacement suggestions");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting replacement suggestions"));
            }
        }

        [HttpPost(ApiEndPointConstant.Document.GetReplacementSuggestionsForEdit)]
        [ProducesResponseType(typeof(ApiResponse<DocumentReplacementSuggestionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReplacementSuggestionsForEdit(
            [FromRoute(Name = "documentId")] string documentId,
            [FromBody] DocumentReplacementSuggestionRequest request)
        {
            try
            {
                _logger.LogInformation("Getting replacement suggestions for editing document {DocumentId}",
                    documentId);

                var result = await _replacementService.GetReplacementSuggestionsForEditAsync(documentId, request);

                return Ok(ApiResponse<DocumentReplacementSuggestionResponse>.Success(
                    result,
                    $"Found {result.TotalFound} replacement suggestions for editing",
                    StatusCodes.Status200OK));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting replacement suggestions for editing document {DocumentId}",
                    documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting replacement suggestions"));
            }
        }

        [HttpPost(ApiEndPointConstant.Document.GetReplacementScoringBreakdown)]
        [ProducesResponseType(typeof(ApiResponse<ReplacementSuggestionScoring>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReplacementScoringBreakdown(
            [FromRoute(Name = "candidateId")] string candidateId,
            [FromBody] DocumentReplacementSuggestionRequest request)
        {
            try
            {
                _logger.LogInformation("Getting scoring breakdown for candidate {CandidateId}",
                    candidateId);

                var result = await _replacementService.GetScoringBreakdownAsync(request, candidateId);

                return Ok(ApiResponse<ReplacementSuggestionScoring>.Success(
                    result,
                    "Scoring breakdown retrieved successfully",
                    StatusCodes.Status200OK));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scoring breakdown for candidate {CandidateId}",
                    candidateId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while getting scoring breakdown"));
            }
        }

        [HttpGet(ApiEndPointConstant.Document.ValidateReplacement)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateReplacement(
            [FromRoute(Name = "documentId")] string documentId)
        {
            try
            {
                _logger.LogInformation("Validating replacement permission for document {DocumentId}",
                    documentId);

                var canReplace = await _replacementService.CanReplaceDocumentAsync(documentId);

                return Ok(ApiResponse<bool>.Success(
                    canReplace,
                    canReplace ? "Document can be replaced" : "Document cannot be replaced",
                    StatusCodes.Status200OK));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating replacement for document {DocumentId}",
                    documentId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while validating replacement"));
            }
        }

        [HttpDelete(ApiEndPointConstant.ApiEndpoint + "/replacement-suggestions/cache")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ClearReplacementCache(
            [FromQuery] string? documentTypeId = null,
            [FromQuery] string? departmentId = null)
        {
            try
            {
                _logger.LogInformation("Clearing replacement cache for type {DocumentTypeId}, department {DepartmentId}", 
                    documentTypeId, departmentId);

                if (!string.IsNullOrEmpty(documentTypeId) && !string.IsNullOrEmpty(departmentId))
                {
                    await _replacementService.ClearReplacementCacheAsync(documentTypeId, departmentId);
                }
                else
                {
                    await _replacementService.ClearAllReplacementCachesAsync();
                }
                
                return Ok(ApiResponse<object>.Success(
                    null, 
                    "Replacement cache cleared successfully", 
                    StatusCodes.Status200OK));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing replacement cache");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error("INTERNAL_ERROR", "An error occurred while clearing cache"));
            }
        }
    }
}
