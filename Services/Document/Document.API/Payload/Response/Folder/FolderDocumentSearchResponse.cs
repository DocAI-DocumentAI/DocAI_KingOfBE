using Document.API.Payload.Response.Document;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder-based document search
    /// </summary>
    public class FolderDocumentSearchResponse
    {
        /// <summary>
        /// Search context folder information
        /// </summary>
        public FolderSummaryResponse SearchFolder { get; set; }

        /// <summary>
        /// Search results
        /// </summary>
        public List<DocumentSearchResultResponse> Documents { get; set; } = new List<DocumentSearchResultResponse>();

        /// <summary>
        /// Total number of matching documents
        /// </summary>
        public int TotalResults { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Search query used
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Search type used (FullText, Semantic)
        /// </summary>
        public string SearchType { get; set; }

        /// <summary>
        /// Whether subfolders were included in search
        /// </summary>
        public bool IncludedSubfolders { get; set; }

        /// <summary>
        /// Applied filters
        /// </summary>
        public FolderSearchFilters? AppliedFilters { get; set; }

        /// <summary>
        /// Search execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Folders that were searched (when including subfolders)
        /// </summary>
        public List<FolderSummaryResponse> SearchedFolders { get; set; } = new List<FolderSummaryResponse>();
    }

    /// <summary>
    /// Document search result with folder context
    /// </summary>
    public class DocumentSearchResultResponse : DocumentSummaryResponse
    {
        /// <summary>
        /// Folder containing this document
        /// </summary>
        public FolderSummaryResponse? ContainingFolder { get; set; }

        /// <summary>
        /// Search relevance score (for semantic search)
        /// </summary>
        public double? RelevanceScore { get; set; }

        /// <summary>
        /// Highlighted text snippets matching the search
        /// </summary>
        public List<string> HighlightedSnippets { get; set; } = new List<string>();

        /// <summary>
        /// Matching fields (title, content, tags, etc.)
        /// </summary>
        public List<string> MatchingFields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Applied filters for folder-based search
    /// </summary>
    public class FolderSearchFilters
    {
        /// <summary>
        /// Document status filter
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Document type filter
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Document type name (for display)
        /// </summary>
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Tags filter
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Date range - from
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Date range - to
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Signed by filter
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Sort field
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>
        /// Sort direction
        /// </summary>
        public string? SortDirection { get; set; }
    }
}
