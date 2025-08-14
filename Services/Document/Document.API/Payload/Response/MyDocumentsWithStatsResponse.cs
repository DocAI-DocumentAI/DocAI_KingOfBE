using Document.Infrastructure.Paginate;

namespace Document.API.Payload.Response
{
    /// <summary>
    /// Enhanced response model for user's documents with status statistics
    /// </summary>
    public class MyDocumentsWithStatsResponse
    {
        /// <summary>
        /// Paginated list of user's documents
        /// </summary>
        public IPaginate<DocumentDraftResponse> Documents { get; set; } = null!;

        /// <summary>
        /// Summary statistics for the user's documents by status
        /// </summary>
        public MyDocumentsStatistics Statistics { get; set; } = null!;
    }

    /// <summary>
    /// Summary statistics for user's documents by status
    /// </summary>
    public class MyDocumentsStatistics
    {
        /// <summary>
        /// Total number of draft documents
        /// </summary>
        public int TotalDrafts { get; set; }

        /// <summary>
        /// Total number of pending documents (submitted for approval)
        /// </summary>
        public int TotalPending { get; set; }

        /// <summary>
        /// Total number of approved documents
        /// </summary>
        public int TotalApproved { get; set; }

        /// <summary>
        /// Total number of rejected documents
        /// </summary>
        public int TotalRejected { get; set; }

        /// <summary>
        /// Total number of archived documents
        /// </summary>
        public int TotalArchived { get; set; }

        /// <summary>
        /// Total number of all documents
        /// </summary>
        public int TotalDocuments { get; set; }
    }
}
