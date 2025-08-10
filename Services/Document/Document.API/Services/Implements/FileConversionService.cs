using Document.API.Services.Interfaces;

namespace Document.API.Services.Implements
{
    public class FileConversionService : IFileConversionService
    {
        private readonly ILogger<FileConversionService> _logger;

        public FileConversionService(ILogger<FileConversionService> logger)
        {
            _logger = logger;
        }

        public bool CanViewInline(string fileExtension)
        {
            var extension = fileExtension.ToLowerInvariant();
            return extension switch
            {
                ".pdf" => true,
                ".txt" => true,
                ".jpg" or ".jpeg" => true,
                ".png" => true,
                ".gif" => true,
                ".svg" => true,
                ".html" => true,
                ".htm" => true,
                _ => false
            };
        }

        public string GetContentType(string fileExtension)
        {
            var extension = fileExtension.ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".xml" => "application/xml",
                ".json" => "application/json",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                _ => "application/octet-stream"
            };
        }

        public string GetContentDisposition(string fileExtension, string fileName, bool forceDownload = false)
        {
            if (forceDownload || !CanViewInline(fileExtension))
            {
                return $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            }
            else
            {
                return $"inline; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            }
        }

        public async Task<(Stream stream, string contentType)> ConvertDocxToPdfAsync(Stream docxStream)
        {
            // This is a placeholder for future DOCX to PDF conversion
            // You could implement this using libraries like:
            // - Aspose.Words
            // - DocumentFormat.OpenXml + iTextSharp
            // - LibreOffice headless conversion
            // - Azure Form Recognizer
            
            _logger.LogWarning("DOCX to PDF conversion is not yet implemented. Returning original stream.");
            
            // For now, return the original stream
            docxStream.Position = 0;
            return (docxStream, GetContentType(".docx"));
        }

        public Dictionary<string, string> GetSecurityHeaders(string fileExtension)
        {
            var headers = new Dictionary<string, string>
            {
                ["X-Content-Type-Options"] = "nosniff",
                ["X-Frame-Options"] = "SAMEORIGIN",
                ["X-XSS-Protection"] = "1; mode=block",
                ["Referrer-Policy"] = "strict-origin-when-cross-origin"
            };

            var extension = fileExtension.ToLowerInvariant();
            
            switch (extension)
            {
                case ".pdf":
                    // Allow PDF to be embedded in frames from same origin
                    headers["Content-Security-Policy"] = "default-src 'self'; object-src 'self'; frame-ancestors 'self'";
                    break;
                    
                case ".html":
                case ".htm":
                    // Strict CSP for HTML files
                    headers["Content-Security-Policy"] = "default-src 'none'; script-src 'none'; object-src 'none'";
                    break;
                    
                case ".svg":
                    // Prevent SVG from executing scripts
                    headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
                    break;
                    
                case ".txt":
                case ".csv":
                    // Basic protection for text files
                    headers["Content-Security-Policy"] = "default-src 'none'";
                    break;
                    
                default:
                    // Default CSP for other file types
                    headers["Content-Security-Policy"] = "default-src 'none'";
                    break;
            }

            return headers;
        }


    }
}
