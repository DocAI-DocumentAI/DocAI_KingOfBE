using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Infrastructure.Paginate;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for managing document types
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class DocumentTypeController : ControllerBase
    {
        private readonly IDocumentTypeService _documentTypeService;
        private readonly ILogger<DocumentTypeController> _logger;

        public DocumentTypeController(
            IDocumentTypeService documentTypeService,
            ILogger<DocumentTypeController> logger)
        {
            _documentTypeService = documentTypeService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new document type
        /// </summary>
        /// <param name="request">The document type creation request</param>
        /// <param name="userId">The ID of the user creating the document type</param>
        /// <returns>The created document type</returns>
        [HttpPost(ApiEndPointConstant.DocumentType.CreateDocumentType)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<DocumentTypeResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateDocumentType([FromBody] CreateDocumentTypeRequest request, string userId)
        {
            _logger.LogInformation("Creating document type with name: {Name} by user: {UserId}", request.Name, userId);
            
            var result = await _documentTypeService.CreateDocumentTypeAsync(request, userId);
            return Ok(ApiResponse<DocumentTypeResponse>.Success(result, "Document type created successfully", 201));
        }

        /// <summary>
        /// Gets a document type by its ID
        /// </summary>
        /// <param name="id">The ID of the document type</param>
        /// <returns>The document type information</returns>
        [HttpGet(ApiEndPointConstant.DocumentType.GetDocumentTypeById)]
        [ProducesResponseType(typeof(ApiResponse<DocumentTypeResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentTypeById([FromRoute(Name = "id")] string id)
        {
            _logger.LogInformation("Retrieving document type with ID: {DocumentTypeId}", id);
            
            var result = await _documentTypeService.GetDocumentTypeByIdAsync(id);
            return Ok(ApiResponse<DocumentTypeResponse>.Success(result));
        }

        /// <summary>
        /// Gets all document types with pagination
        /// </summary>
        /// <param name="pageNumber">The page number (default: 1)</param>
        /// <param name="pageSize">The page size (default: 10)</param>
        /// <returns>Paginated list of document types</returns>
        [HttpGet(ApiEndPointConstant.DocumentType.GetAllDocumentTypes)]
        [ProducesResponseType(typeof(ApiResponse<IPaginate<DocumentTypeResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllDocumentTypes(int pageNumber = 1, int pageSize = 10)
        {
            _logger.LogInformation("Retrieving document types - Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);
            
            var result = await _documentTypeService.GetAllDocumentTypesAsync(pageNumber, pageSize);
            return Ok(ApiResponse<IPaginate<DocumentTypeResponse>>.Success(result));
        }

        /// <summary>
        /// Gets all document types as a simple list (for dropdown/selection purposes)
        /// </summary>
        /// <returns>List of document types</returns>
        [HttpGet(ApiEndPointConstant.DocumentType.GetDocumentTypesList)]
        [ProducesResponseType(typeof(ApiResponse<List<DocumentTypeResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentTypesList()
        {
            _logger.LogInformation("Retrieving document types list for selection");
            
            var result = await _documentTypeService.GetDocumentTypesListAsync();
            return Ok(ApiResponse<List<DocumentTypeResponse>>.Success(result));
        }

        /// <summary>
        /// Updates an existing document type
        /// </summary>
        /// <param name="id">The ID of the document type to update</param>
        /// <param name="request">The document type update request</param>
        /// <param name="userId">The ID of the user updating the document type</param>
        /// <returns>The updated document type</returns>
        [HttpPut(ApiEndPointConstant.DocumentType.UpdateDocumentType)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<DocumentTypeResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDocumentType([FromRoute(Name = "id")] string id, [FromBody] UpdateDocumentTypeRequest request, string userId)
        {
            _logger.LogInformation("Updating document type with ID: {DocumentTypeId} by user: {UserId}", id, userId);
            
            var result = await _documentTypeService.UpdateDocumentTypeAsync(id, request, userId);
            return Ok(ApiResponse<DocumentTypeResponse>.Success(result, "Document type updated successfully"));
        }

        /// <summary>
        /// Deletes a document type
        /// </summary>
        /// <param name="id">The ID of the document type to delete</param>
        /// <param name="userId">The ID of the user deleting the document type</param>
        /// <returns>Success response</returns>
        [HttpDelete(ApiEndPointConstant.DocumentType.DeleteDocumentType)]
        [CustomAuthorize(Roles = new[] { Roles.Manager, Roles.Admin })]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDocumentType([FromRoute(Name = "id")] string id, string userId)
        {
            _logger.LogInformation("Deleting document type with ID: {DocumentTypeId} by user: {UserId}", id, userId);
            
            await _documentTypeService.DeleteDocumentTypeAsync(id, userId);
            return Ok(ApiResponse<object>.Success(null, "Document type deleted successfully"));
        }
    }
}
