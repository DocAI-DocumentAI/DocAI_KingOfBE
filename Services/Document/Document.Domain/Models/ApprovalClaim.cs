using System.ComponentModel.DataAnnotations;

namespace Document.Domain.Models
{
    /// <summary>
    /// Represents a claim on a document for approval review
    /// </summary>
    public class ApprovalClaim
    {
        /// <summary>
        /// Unique identifier for the claim
        /// </summary>
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Document version being claimed
        /// </summary>
        [Required]
        public string DocumentVersionId { get; set; }

        /// <summary>
        /// Navigation property to document version
        /// </summary>
        public DocumentVersion DocumentVersion { get; set; }

        /// <summary>
        /// User who claimed the document
        /// </summary>
        [Required]
        public string ClaimedBy { get; set; }

        /// <summary>
        /// When the document was claimed
        /// </summary>
        public DateTime ClaimedAt { get; set; }

        /// <summary>
        /// Whether the claim is still active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// User who created the claim
        /// </summary>
        [Required]
        public string CreatedBy { get; set; }

        /// <summary>
        /// User who last updated the claim
        /// </summary>
        public string? LastUpdatedBy { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }
    }
}