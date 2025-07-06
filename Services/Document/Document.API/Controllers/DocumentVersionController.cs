using Document.API.Constants;
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
        public async Task<IActionResult> GetDocumentVersions([FromRoute(Name = "id")] string documentId)
        {
            var result = await _documentService.GetDocumentVersionsAsync(documentId);
            return Ok(ApiResponse<object>.Success(result, "Document versions retrieved successfully"));
        }

        [HttpGet(ApiEndPointConstant.DocumentVersion.GetDocumentVersion)]
        public async Task<IActionResult> GetDocumentVersion([FromRoute(Name = "id")] string documentId, [FromRoute(Name = "versionId")] string versionId)
        {
            var result = await _documentService.GetDocumentVersionAsync(documentId, versionId);
            return Ok(ApiResponse<object>.Success(result, "Document version retrieved successfully"));
        }
    }
}
