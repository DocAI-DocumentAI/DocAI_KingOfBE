using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for managing document versions and version history
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class DocumentVersionController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentVersionController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        /// <summary>
        /// Get all versions of a specific document
        /// </summary>
        /// <param name="documentId">The ID of the document to get versions for</param>
        /// <returns>List of all document versions with metadata</returns>
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

        /// <summary>
        /// Get a specific version of a document
        /// </summary>
        /// <param name="documentId">The ID of the document</param>
        /// <param name="versionId">The ID of the specific version to retrieve</param>
        /// <returns>Specific document version details</returns>
        [HttpGet(ApiEndPointConstant.DocumentVersion.GetDocumentVersion)]
        [ProducesResponseType(typeof(ApiResponse<DocumentVersionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentVersion([FromRoute(Name = "id")] string documentId, [FromRoute(Name = "versionId")] string versionId)
        {
            var result = await _documentService.GetDocumentVersionByVersionIdAsync(documentId, versionId);
            return Ok(ApiResponse<object>.Success(result, "Document version retrieved successfully"));
        }
    }
}
