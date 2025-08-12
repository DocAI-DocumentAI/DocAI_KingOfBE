using Document.Infrastructure.Paginate;

namespace Document.API.Payload.Response
{
    /// <summary>
    /// Enhanced response model for approval queue with summary statistics and comprehensive filtering
    /// </summary>
    public class ApprovalQueueSummaryResponse
    {
        /// <summary>
        /// Paginated list of pending documents
        /// </summary>
        public IPaginate<PendingDocumentResponse> Documents { get; set; } = null!;

        /// <summary>
        /// Summary statistics for the manager's department approval queue
        /// </summary>
        public ApprovalQueueStatistics Statistics { get; set; } = null!;
    }

    /// <summary>
    /// Summary statistics for approval queue management
    /// </summary>
    public class ApprovalQueueStatistics
    {
        /// <summary>
        /// Total number of pending documents in the department
        /// </summary>
        public int TotalPending { get; set; }

        /// <summary>
        /// Total number of approved documents in the department
        /// </summary>
        public int TotalApproved { get; set; }

        /// <summary>
        /// Total number of rejected documents in the department
        /// </summary>
        public int TotalRejected { get; set; }

        /// <summary>
        /// Total number of archived documents in the department
        /// </summary>
        public int TotalArchived { get; set; }

        /// <summary>
        /// Total number of documents currently being reviewed (claimed)
        /// </summary>
        public int TotalBeingReviewed { get; set; }

        /// <summary>
        /// Number of documents submitted in the last 7 days
        /// </summary>
        public int RecentSubmissions { get; set; }

        /// <summary>
        /// Number of documents approaching expiration (within 2 days of 7-day timeout)
        /// </summary>
        public int ApproachingExpiration { get; set; }

        /// <summary>
        /// Average processing time in hours for approved/rejected documents in the last 30 days
        /// </summary>
        public double AverageProcessingTimeHours { get; set; }
    }
}
