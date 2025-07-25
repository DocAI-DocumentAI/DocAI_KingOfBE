using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response.DocumentServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentServiceClient _documentService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(IDocumentServiceClient documentService, ILogger<DocumentController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        // Core Search
        [HttpPost("search")]
        public async Task<ActionResult<DocumentSearchResponse>> SearchDocuments([FromBody] DocumentSearchRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _documentService.SearchDocumentsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("search/by-ids")]
        public async Task<ActionResult<List<DocumentCitation>>> SearchDocumentsByIds([FromBody] DocumentIdsRequest request)
        {
            try
            {
                if (request.DocumentIds == null || !request.DocumentIds.Any())
                {
                    return BadRequest(new { message = "Document IDs are required" });
                }

                var userId = GetUserId();
                var citations = await _documentService.SearchDocumentsByIdsAsync(request.DocumentIds, userId);
                return Ok(citations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents by IDs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Access Control
        [HttpPost("access/check")]
        public async Task<ActionResult<DocumentAccessResponse>> CheckDocumentAccess([FromBody] DocumentAccessRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _documentService.CheckDocumentAccessAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking document access");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("access/batch")]
        public async Task<ActionResult<BatchDocumentResponse>> CheckBatchAccess([FromBody] BatchDocumentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _documentService.CheckBatchAccessAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking batch document access");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Document Management
        [HttpGet("{documentId}/metadata")]
        public async Task<ActionResult<DocumentMetadata>> GetDocumentMetadata(string documentId)
        {
            try
            {
                if (string.IsNullOrEmpty(documentId))
                {
                    return BadRequest(new { message = "Document ID is required" });
                }

                var userId = GetUserId();
                var metadata = await _documentService.GetDocumentMetadataAsync(documentId, userId);
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document metadata for {DocumentId}", documentId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("metadata/batch")]
        public async Task<ActionResult<BatchDocumentResponse>> GetBatchMetadata([FromBody] BatchDocumentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _documentService.GetBatchMetadataAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting batch document metadata");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{documentId}/content")]
        public async Task<ActionResult<DocumentContent>> GetDocumentContent(string documentId)
        {
            try
            {
                if (string.IsNullOrEmpty(documentId))
                {
                    return BadRequest(new { message = "Document ID is required" });
                }

                var userId = GetUserId();
                var content = await _documentService.GetDocumentContentAsync(documentId, userId);
                return Ok(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document content for {DocumentId}", documentId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Status & Health
        [HttpPost("status")]
        public async Task<ActionResult<DocumentStatusResponse>> CheckDocumentStatus([FromBody] DocumentStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _documentService.CheckDocumentStatusAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking document status");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<List<string>>> GetDocumentCategories()
        {
            try
            {
                var categories = await _documentService.GetDocumentCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document categories");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }

    // Helper request classes
    public class DocumentIdsRequest
    {
        public List<string> DocumentIds { get; set; } = new();
    }
}
