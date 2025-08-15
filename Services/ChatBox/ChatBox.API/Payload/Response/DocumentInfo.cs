namespace ChatBox.API.Payload.Response
{
    public class DocumentInfo
    {
        public string DocumentId { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string VersionName { get; set; }

        // Legal & Approval Info
        public string SignedBy { get; set; }
        public string OwnerName { get; set; }
        public string CreatedBy { get; set; }
        public string ReviewerName { get; set; }
        public string ApprovedBy { get; set; }

        // Organizational Info
        public string DepartmentId { get; set; }
        public string DepartmentName { get; set; }

        public bool IsPublic { get; set; } = false;

        // Temporal Info
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? SignedDate { get; set; }
        public DateTime? ReviewDate { get; set; }

        // Content Info
        public string Summary { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        // File Info
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public string FileName { get; set; }

        // Document Classification
        public string DocumentType { get; set; }
        public string Status { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }

        // Search & Relevance
        public double RelevanceScore { get; set; }

        // Version Info
        public bool IsLatestVersion { get; set; }
        public int VersionNumber { get; set; }

        // Access Control
        public string Visibility { get; set; }
        public string PermissionLevel { get; set; }

        // Relationships
        public string ParentDocumentId { get; set; }
        public List<string> RelatedDocumentIds { get; set; } = new List<string>();
    }
}
