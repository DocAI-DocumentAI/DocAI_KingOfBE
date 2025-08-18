using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for browsing folder contents (documents and subfolders)
    /// </summary>
    public class FolderBrowseRequest
    {
        /// <summary>
        /// Folder ID to browse (null for root level)
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// Department ID for department-specific browsing
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Whether to browse public folders
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Include subfolders in the response
        /// </summary>
        public bool IncludeSubfolders { get; set; } = true;

        /// <summary>
        /// Include documents in the response
        /// </summary>
        public bool IncludeDocuments { get; set; } = true;

        /// <summary>
        /// Maximum depth for subfolder inclusion
        /// </summary>
        [Range(1, 5, ErrorMessage = "Max depth must be between 1 and 5")]
        public int MaxDepth { get; set; } = 1;

        /// <summary>
        /// Document status filter for included documents
        /// </summary>
        public string? DocumentStatus { get; set; }

        /// <summary>
        /// Document type filter
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Page number for document pagination (1-based)
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int DocumentPage { get; set; } = 1;

        /// <summary>
        /// Page size for document pagination
        /// </summary>
        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int DocumentPageSize { get; set; } = 20;

        /// <summary>
        /// Sort field for documents
        /// </summary>
        public string? DocumentSortBy { get; set; } = "LastUpdatedTime";

        /// <summary>
        /// Sort direction for documents (asc, desc)
        /// </summary>
        public string? DocumentSortDirection { get; set; } = "desc";

        /// <summary>
        /// Sort field for folders
        /// </summary>
        public string? FolderSortBy { get; set; } = "Name";

        /// <summary>
        /// Sort direction for folders (asc, desc)
        /// </summary>
        public string? FolderSortDirection { get; set; } = "asc";
    }
}
