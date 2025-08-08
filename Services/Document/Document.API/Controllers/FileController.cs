using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for file operations including viewing, downloading, and file information
    /// </summary>
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    [CustomAuthorize]
    public class FileController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IFileConversionService _fileConversionService;
        private readonly IGoogleDriveService _googleDriveService;

        public FileController(
            IDocumentService documentService,
            IFileConversionService fileConversionService,
            IGoogleDriveService googleDriveService)
        {
            _documentService = documentService;
            _fileConversionService = fileConversionService;
            _googleDriveService = googleDriveService;
        }

        /// <summary>
        /// View a document file inline in the browser with proper security headers
        /// </summary>
        /// <param name="versionId">The version ID of the document to view</param>
        /// <returns>File stream for inline viewing</returns>
        [HttpGet(ApiEndPointConstant.Document.ViewFile)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ViewFile([FromRoute(Name = "versionId")] string versionId)
        {
            var (stream, contentType, fileName) = await _documentService.GetFileForViewingAsync(versionId);

            // Get file extension for proper handling
            var fileExtension = Path.GetExtension(fileName);

            // Use FileConversionService for proper content type and headers
            var properContentType = _fileConversionService.GetContentType(fileExtension);
            var contentDisposition = _fileConversionService.GetContentDisposition(fileExtension, fileName, forceDownload: false);
            var securityHeaders = _fileConversionService.GetSecurityHeaders(fileExtension);

            // Set headers
            Response.Headers["Content-Disposition"] = contentDisposition;

            // Add security headers
            foreach (var header in securityHeaders)
            {
                Response.Headers[header.Key] = header.Value;
            }

            return File(stream, properContentType, fileName);
        }

        /// <summary>
        /// Download a document file with proper headers for file download
        /// </summary>
        /// <param name="versionId">The version ID of the document to download</param>
        /// <returns>File stream for download</returns>
        [HttpGet(ApiEndPointConstant.Document.DownloadFile)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadFile([FromRoute(Name = "versionId")] string versionId)
        {
            var (stream, contentType, fileName) = await _documentService.GetFileForDownloadAsync(versionId);

            // Get file extension for proper handling
            var fileExtension = Path.GetExtension(fileName);

            // Use FileConversionService for proper content type and headers
            var properContentType = _fileConversionService.GetContentType(fileExtension);
            var contentDisposition = _fileConversionService.GetContentDisposition(fileExtension, fileName, forceDownload: true);
            var securityHeaders = _fileConversionService.GetSecurityHeaders(fileExtension);

            // Set headers for download
            Response.Headers["Content-Disposition"] = contentDisposition;

            // Add security headers
            foreach (var header in securityHeaders)
            {
                Response.Headers[header.Key] = header.Value;
            }

            return File(stream, properContentType, fileName);
        }

        /// <summary>
        /// Get detailed information about a document file including metadata and URLs
        /// </summary>
        /// <param name="versionId">The version ID of the document</param>
        /// <returns>File information including size, type, and access URLs</returns>
        [HttpGet(ApiEndPointConstant.Document.GetFileInfo)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFileInfo([FromRoute(Name = "versionId")] string versionId)
        {
            var version = await _documentService.GetFileInfoAsync(versionId);

            var fileInfo = new
            {
                VersionId = version.Id,
                DocumentId = version.DocumentFileId,
                FileName = version.FileName,
                FileSize = version.FileSize,
                FileType = version.FileType,
                ContentType = _fileConversionService.GetContentType(version.FileType),
                Status = version.Status.ToString(),
                CreatedTime = version.CreatedTime,
                LastModified = version.LastUpdatedTime,
                CanView = _fileConversionService.CanViewInline(version.FileType),
                ViewUrl = Url.Action(nameof(ViewFile), new { versionId = version.Id }),
                DownloadUrl = Url.Action(nameof(DownloadFile), new { versionId = version.Id })
            };

            return Ok(ApiResponse<object>.Success(fileInfo, "File information retrieved successfully"));
        }

        /// <summary>
        /// Generate a secure iframe URL for viewing a document in the browser
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>Iframe viewing URL and metadata</returns>
        [HttpGet(ApiEndPointConstant.Document.GetIframeViewingUrl)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<IframeViewingResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetIframeUrl([FromRoute(Name = "versionId")] string versionId)
        {
            var response = await _googleDriveService.GetIframeViewingUrlAsync(versionId);
            return response.StatusCode >= 200 && response.StatusCode < 300
                ? Ok(response)
                : StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Generate a time-limited sharing link for a document
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <param name="expirationHours">Hours until link expires (default: 24, max: 168)</param>
        /// <returns>Time-limited sharing URL and metadata</returns>
        [HttpGet(ApiEndPointConstant.Document.GetSharingLink)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<SharingLinkResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSharingLink(
            [FromRoute(Name = "versionId")] string versionId,
            [FromQuery] int expirationHours = 24)
        {
            var response = await _googleDriveService.GetSharingLinkAsync(versionId, expirationHours);
            return response.StatusCode >= 200 && response.StatusCode < 300
                ? Ok(response)
                : StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Validate user access to a document with comprehensive metadata
        /// </summary>
        /// <param name="versionId">Document version ID</param>
        /// <returns>Access validation result with detailed information</returns>
        [HttpGet(ApiEndPointConstant.Document.ValidateFileAccess)]
        [CustomAuthorize]
        [ProducesResponseType(typeof(ApiResponse<FileAccessValidationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateAccess([FromRoute(Name = "versionId")] string versionId)
        {
            var response = await _googleDriveService.ValidateDocumentAccessAsync(versionId);
            return response.StatusCode >= 200 && response.StatusCode < 300
                ? Ok(response)
                : StatusCode(response.StatusCode, response);
        }


    }
}
