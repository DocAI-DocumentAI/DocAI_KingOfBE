using Document.API.Constants;
using Document.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Controllers
{
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IFileConversionService _fileConversionService;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IDocumentService documentService,
            IFileConversionService fileConversionService,
            ILogger<FileController> logger)
        {
            _documentService = documentService;
            _fileConversionService = fileConversionService;
            _logger = logger;
        }

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


    }
}
