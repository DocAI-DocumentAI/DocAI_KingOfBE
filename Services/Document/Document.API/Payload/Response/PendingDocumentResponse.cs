using Document.Domain.Enums;

namespace Document.API.Payload.Response
{
    public class PendingDocumentResponse
    {
        public string DocumentFileId { get; set; }
        public string VersionId { get; set; }
        public string VersionName { get; set; }
        public string Title { get; set; }
        public string SubmittedBy { get; set; }
        public string? SubmittedByName { get; set; }
        public DateTime LastSubmitted { get; set; }
        public string Status { get; set; }
        public string DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }

        /// <summary>
        /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
        /// </summary>
        public bool IsPublic { get; set; }
    }
}
