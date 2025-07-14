using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class DocumentVersionController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentVersionController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpGet(ApiEndPointConstant.DocumentVersion.GetDocumentVersions)]
        [ProducesResponseType(typeof(ApiResponse<List<DocumentVersionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentVersions([FromRoute(Name = "id")] string documentId)
        {
            var result = await _documentService.GetDocumentVersionsAsync(documentId);
            return Ok(ApiResponse<object>.Success(result, "Document versions retrieved successfully"));
        }

        [HttpGet(ApiEndPointConstant.DocumentVersion.GetDocumentVersion)]
        [ProducesResponseType(typeof(ApiResponse<DocumentVersionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentVersion([FromRoute(Name = "id")] string documentId, [FromRoute(Name = "versionId")] string versionId)
        {
            var result = await _documentService.GetDocumentVersionAsync(documentId, versionId);
            return Ok(ApiResponse<object>.Success(result, "Document version retrieved successfully"));
        }
    }
}
