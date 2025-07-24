namespace Document.API.Services.Interfaces
{
    public interface IFileConversionService
    {
        /// <summary>
        /// Determines if a file type can be viewed inline in the browser
        /// </summary>
        bool CanViewInline(string fileExtension);

        /// <summary>
        /// Gets the appropriate content type for a file extension
        /// </summary>
        string GetContentType(string fileExtension);

        /// <summary>
        /// Gets the appropriate content disposition for a file type
        /// </summary>
        string GetContentDisposition(string fileExtension, string fileName, bool forceDownload = false);

        /// <summary>
        /// Converts DOCX to PDF for inline viewing (future enhancement)
        /// </summary>
        Task<(Stream stream, string contentType)> ConvertDocxToPdfAsync(Stream docxStream);

        /// <summary>
        /// Gets security headers appropriate for the file type
        /// </summary>
        Dictionary<string, string> GetSecurityHeaders(string fileExtension);
    }
}
