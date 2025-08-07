namespace Document.API.Payload.Response
{
    public class DocumentReplacementCandidate
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double SimilarityScore { get; set; }
        public double SemanticScore { get; set; }
        public double MetadataScore { get; set; }
        public double ContextScore { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public bool CanReplace { get; set; }
        public string? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string DocumentTypeId { get; set; } = string.Empty;
        public string? DocumentTypeName { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedTime { get; set; }
        public string? LastUpdatedBy { get; set; }
        public string? LastUpdatedByName { get; set; }
        public DateTime? LastUpdatedTime { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public int SharedTagCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public long? FileSize { get; set; }
        public string? FileType { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public string? SignedBy { get; set; }
        public string? Summary { get; set; }
    }
}
