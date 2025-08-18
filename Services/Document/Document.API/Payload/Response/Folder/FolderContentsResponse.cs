using Document.API.Payload.Response.Document;
using Document.Domain.Models;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder contents (documents and subfolders)
    /// </summary>
    public class FolderContentsResponse
    {
        /// <summary>
        /// Current folder information
        /// </summary>
        public FolderDetailResponse? CurrentFolder { get; set; }

        /// <summary>
        /// Parent folder information (null if at root)
        /// </summary>
        public FolderSummaryResponse? ParentFolder { get; set; }

        /// <summary>
        /// Breadcrumb navigation path
        /// </summary>
        public List<FolderBreadcrumbResponse> Breadcrumb { get; set; } = new List<FolderBreadcrumbResponse>();

        /// <summary>
        /// Subfolders in the current folder
        /// </summary>
        public List<FolderSummaryResponse> SubFolders { get; set; } = new List<FolderSummaryResponse>();

        /// <summary>
        /// Documents in the current folder
        /// </summary>
        public List<DocumentSummaryResponse> Documents { get; set; } = new List<DocumentSummaryResponse>();

        /// <summary>
        /// Total number of subfolders (for pagination)
        /// </summary>
        public int TotalSubFolders { get; set; }

        /// <summary>
        /// Total number of documents (for pagination)
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Current page for documents
        /// </summary>
        public int CurrentDocumentPage { get; set; }

        /// <summary>
        /// Page size for documents
        /// </summary>
        public int DocumentPageSize { get; set; }

        /// <summary>
        /// Total pages for documents
        /// </summary>
        public int TotalDocumentPages { get; set; }

        /// <summary>
        /// User's permissions on the current folder
        /// </summary>
        public FolderActionPermissions? UserPermissions { get; set; }

        /// <summary>
        /// Applied filters
        /// </summary>
        public FolderBrowseFilters? AppliedFilters { get; set; }

        /// <summary>
        /// Sorting information
        /// </summary>
        public FolderBrowseSorting? Sorting { get; set; }
    }

    /// <summary>
    /// Applied filters for folder browsing
    /// </summary>
    public class FolderBrowseFilters
    {
        /// <summary>
        /// Document status filter
        /// </summary>
        public string? DocumentStatus { get; set; }

        /// <summary>
        /// Document type filter
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Document type name (for display)
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Whether subfolders are included
        /// </summary>
        public bool IncludeSubfolders { get; set; }

        /// <summary>
        /// Maximum depth for subfolders
        /// </summary>
        public int MaxDepth { get; set; }
    }

    /// <summary>
    /// Sorting information for folder browsing
    /// </summary>
    public class FolderBrowseSorting
    {
        /// <summary>
        /// Document sort field
        /// </summary>
        public string? DocumentSortBy { get; set; }

        /// <summary>
        /// Document sort direction
        /// </summary>
        public string? DocumentSortDirection { get; set; }

        /// <summary>
        /// Folder sort field
        /// </summary>
        public string? FolderSortBy { get; set; }

        /// <summary>
        /// Folder sort direction
        /// </summary>
        public string? FolderSortDirection { get; set; }
    }
}
