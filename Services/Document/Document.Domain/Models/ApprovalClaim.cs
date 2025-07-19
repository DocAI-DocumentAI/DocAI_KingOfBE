namespace Document.Domain.Models
{
    public class ApprovalClaim
    {
        public string DocumentVersionId { get; set; }
        public DocumentVersion DocumentVersion { get; set; }
        public string ClaimedBy { get; set; }
        public DateTime ClaimedAt { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdatedTime { get; set; }
    }
}