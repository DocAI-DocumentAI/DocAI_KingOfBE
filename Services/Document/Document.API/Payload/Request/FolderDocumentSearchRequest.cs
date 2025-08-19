using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for searching documents within a folder context
    /// </summary>
    public class FolderDocumentSearchRequest
    {
        /// <summary>
        /// Folder ID to search within (required)
        /// </summary>
        [Required(ErrorMessage = "Folder ID is required")]
        public string FolderId { get; set; }

        /// <summary>
        /// Search keyword for document content
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// Include documents from subfolders
        /// </summary>
        public bool IncludeSubfolders { get; set; } = false;

        /// <summary>
        /// Document status filter
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Document type filter
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Tags to filter by
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Date range - from date
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Date range - to date
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Signed by filter
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination
        /// </summary>
        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Sort field (Title, CreatedTime, LastUpdatedTime, etc.)
        /// </summary>
        public string? SortBy { get; set; } = "LastUpdatedTime";

        /// <summary>
        /// Sort direction (asc, desc)
        /// </summary>
        public string? SortDirection { get; set; } = "desc";

        /// <summary>
        /// Search type (FullText, Semantic)
        /// </summary>
        public string SearchType { get; set; } = "FullText";
    }
}
