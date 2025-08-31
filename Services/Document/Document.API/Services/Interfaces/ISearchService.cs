using Document.API.Payload.Request;
using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for performing document search using Kernel Memory with natural language queries
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Performs a natural language search using Kernel Memory's AskAsync functionality
        /// Returns AI-generated answers with source document citations
        /// </summary>
        /// <param name="request">Search request with query and filters</param>
        /// <param name="filter">Additional search filters</param>
        /// <returns>Search response with AI answer and document sources</returns>
        Task<EnhancedSemanticSearchResponse> SearchWithKernelMemoryAsync(SemanticSearchRequest request, KernelMemorySearchFilter filter);
    }

    /// <summary>
    /// Additional search filters for Kernel Memory semantic search
    /// </summary>
    public class KernelMemorySearchFilter
    {
        /// <summary>
        /// Filter by specific department ID (optional)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Filter by document type ID (optional)
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Filter documents created from this date (optional)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter documents created until this date (optional)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Filter documents effective from this date (optional)
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Filter documents effective until this date (optional)
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }
    }
}
