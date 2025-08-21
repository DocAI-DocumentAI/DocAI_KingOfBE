using Document.API.Payload.Request;
using Document.API.Payload.Response.Folder;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for folder-based document operations
    /// Handles document browsing, searching, and listing within folder context
    /// </summary>
    public interface IFolderDocumentService
    {
        /// <summary>
        /// Browse folder contents (documents and subfolders)
        /// </summary>
        /// <param name="request">Browse request parameters</param>
        /// <returns>Folder contents with documents and subfolders</returns>
        Task<FolderContentsResponse> BrowseFolderContentsAsync(FolderBrowseRequest request);

        /// <summary>
        /// Get detailed information for a document within folder context
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <returns>Detailed document information with folder context</returns>
        Task<FolderDocumentDetailResponse> GetDocumentDetailAsync(string documentVersionId);

        /// <summary>
        /// Search documents within a specific folder
        /// </summary>
        /// <param name="request">Search request parameters</param>
        /// <returns>Search results within folder context</returns>
        Task<FolderDocumentSearchResponse> SearchDocumentsInFolderAsync(FolderDocumentSearchRequest request);

        /// <summary>
        /// Get ALL documents in a folder with filtering and sorting (no pagination)
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="page">Page number (ignored - returns all documents)</param>
        /// <param name="pageSize">Page size (ignored - returns all documents)</param>
        /// <param name="status">Document status filter</param>
        /// <param name="documentTypeId">Document type filter</param>
        /// <param name="sortBy">Sort field</param>
        /// <param name="sortDirection">Sort direction</param>
        /// <returns>All documents in folder</returns>
        Task<FolderDocumentSearchResponse> GetFolderDocumentsAsync(
            string folderId, 
            int page = 1, 
            int pageSize = 20, 
            string? status = null, 
            string? documentTypeId = null, 
            string? sortBy = "LastUpdatedTime", 
            string? sortDirection = "desc");

        /// <summary>
        /// Get recent documents across accessible folders for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="limit">Maximum number of documents to return</param>
        /// <param name="departmentId">Filter by specific department (optional)</param>
        /// <returns>List of recent documents with folder context</returns>
        Task<List<DocumentSearchResultResponse>> GetRecentDocumentsAsync(
            string userId, 
            string userDepartmentId, 
            int limit = 10, 
            string? departmentId = null);

        /// <summary>
        /// Get document statistics for a folder
        /// </summary>
        /// <param name="folderId">Folder ID</param>
        /// <param name="includeSubfolders">Include subfolders in statistics</param>
        /// <returns>Document statistics</returns>
        Task<FolderDocumentStatistics> GetFolderDocumentStatisticsAsync(string folderId, bool includeSubfolders = false);

        /// <summary>
        /// Move document to a different folder
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <param name="targetFolderId">Target folder ID</param>
        /// <returns>Success status</returns>
        Task<bool> MoveDocumentToFolderAsync(string documentVersionId, string targetFolderId);

        /// <summary>
        /// Bulk move documents to a different folder
        /// </summary>
        /// <param name="documentVersionIds">List of document version IDs</param>
        /// <param name="targetFolderId">Target folder ID</param>
        /// <returns>Number of documents successfully moved</returns>
        Task<int> BulkMoveDocumentsToFolderAsync(List<string> documentVersionIds, string targetFolderId);

        /// <summary>
        /// Get folder path for a document
        /// </summary>
        /// <param name="documentVersionId">Document version ID</param>
        /// <returns>Folder breadcrumb path</returns>
        Task<List<FolderBreadcrumbResponse>> GetDocumentFolderPathAsync(string documentVersionId);

        /// <summary>
        /// Search across multiple folders with advanced filtering
        /// </summary>
        /// <param name="folderIds">List of folder IDs to search</param>
        /// <param name="keyword">Search keyword</param>
        /// <param name="includeSubfolders">Include subfolders in search</param>
        /// <param name="filters">Additional filters</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Cross-folder search results</returns>
        Task<FolderDocumentSearchResponse> SearchAcrossFoldersAsync(
            List<string> folderIds, 
            string? keyword, 
            bool includeSubfolders = false, 
            FolderSearchFilters? filters = null, 
            int page = 1, 
            int pageSize = 20);

        /// <summary>
        /// Get documents by folder path pattern
        /// </summary>
        /// <param name="pathPattern">Folder path pattern (supports wildcards)</param>
        /// <param name="departmentId">Department ID filter</param>
        /// <param name="isPublic">Public folder filter</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Documents matching path pattern</returns>
        Task<FolderDocumentSearchResponse> GetDocumentsByPathPatternAsync(
            string pathPattern, 
            string? departmentId = null, 
            bool? isPublic = null, 
            int page = 1, 
            int pageSize = 20);
    }

    /// <summary>
    /// Document statistics for a folder
    /// </summary>
    public class FolderDocumentStatistics
    {
        /// <summary>
        /// Folder ID
        /// </summary>
        public string FolderId { get; set; }

        /// <summary>
        /// Folder name
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// Total number of documents
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Documents by status
        /// </summary>
        public Dictionary<string, int> DocumentsByStatus { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Documents by type
        /// </summary>
        public Dictionary<string, int> DocumentsByType { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Total file size in bytes
        /// </summary>
        public long TotalFileSize { get; set; }

        /// <summary>
        /// Most recent document date
        /// </summary>
        public DateTime? MostRecentDocument { get; set; }

        /// <summary>
        /// Oldest document date
        /// </summary>
        public DateTime? OldestDocument { get; set; }

        /// <summary>
        /// Number of subfolders included in statistics
        /// </summary>
        public int SubfoldersIncluded { get; set; }

        /// <summary>
        /// Statistics generation date
        /// </summary>
        public DateTime GeneratedAt { get; set; }
    }
}
