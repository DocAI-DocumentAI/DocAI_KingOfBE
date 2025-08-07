namespace Document.API.Payload.Response
{
    public class DocumentSourceResponse
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? FileType { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Person or authority who signed the document
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Effective date from which the document is valid
        /// </summary>
        public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until which the document is valid
        /// </summary>
        public DateTime? EffectiveUntil { get; set; }
    }
}
